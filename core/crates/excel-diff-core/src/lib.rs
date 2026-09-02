//! MacCompare Big-Data Spreadsheet Diff Engine
//! High-throughput tabular diff with calamine, memmap2, csv, and viewport streaming.

use ahash::AHasher;
use calamine::{Data, Reader, open_workbook_auto};
use serde::{Deserialize, Serialize};
use std::collections::{HashMap, HashSet};
use std::fs::File;
use std::hash::{Hash, Hasher};
use std::path::Path;

// MARK: - Data Models

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum ExcelCellType {
    String,
    Number,
    Boolean,
    Date,
    Formula,
    Blank,
    Error,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct ExcelCellData {
    pub column_index: usize,
    pub column_letter: String,
    pub raw_value: String,
    pub formatted_value: String,
    pub formula: Option<String>,
    pub cell_type: ExcelCellType,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum CellDiffStatus {
    Unchanged,
    Added,
    Deleted,
    Modified,
    TypeMismatch,
    FormulaChanged,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct CellDiffDetail {
    pub column_index: usize,
    pub column_letter: String,
    pub left_cell: Option<ExcelCellData>,
    pub right_cell: Option<ExcelCellData>,
    pub status: CellDiffStatus,
    pub diff_reason: Option<String>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum RowDiffStatus {
    Equal,
    LeftOnly,
    RightOnly,
    Modified,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct ExcelRowDiffResult {
    pub row_index_left: Option<usize>,
    pub row_index_right: Option<usize>,
    pub status: RowDiffStatus,
    pub cell_diffs: Vec<CellDiffDetail>,
    pub has_diff: bool,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct ExcelSheetDiffSummary {
    pub sheet_name_left: String,
    pub sheet_name_right: String,
    pub total_rows: usize,
    pub total_columns: usize,
    pub difference_row_count: usize,
    pub equal_row_count: usize,
    pub left_only_row_count: usize,
    pub right_only_row_count: usize,
    pub diff_row_indices: Vec<usize>,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct ExcelWorkbookDiffSummary {
    pub sheet_summaries: Vec<ExcelSheetDiffSummary>,
    pub total_differences: usize,
    pub duration_ms: u64,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ExcelCompareRules {
    pub key_columns: Vec<String>,
    pub numeric_tolerance: f64,
    pub ignore_case: bool,
    pub ignore_whitespace: bool,
    pub ignore_empty_rows: bool,
    pub compare_formulas: bool,
}

impl Default for ExcelCompareRules {
    fn default() -> Self {
        Self {
            key_columns: vec![],
            numeric_tolerance: 0.0001,
            ignore_case: false,
            ignore_whitespace: false,
            ignore_empty_rows: true,
            compare_formulas: false,
        }
    }
}

// MARK: - Internal Tabular Representation

pub struct TabularRow {
    pub cells: Vec<ExcelCellData>,
    pub row_hash: u64,
}

pub struct TabularSheet {
    pub name: String,
    pub headers: Vec<String>,
    pub rows: Vec<TabularRow>,
    pub max_cols: usize,
}

pub struct TabularWorkbook {
    pub sheets: Vec<TabularSheet>,
}

// Helper to convert 0-based column index to Excel column letter (0 -> "A", 27 -> "AB")
pub fn column_letter(col_idx: usize) -> String {
    let mut result = String::new();
    let mut n = col_idx + 1;
    while n > 0 {
        let rem = (n - 1) % 26;
        result.insert(0, (b'A' + rem as u8) as char);
        n = (n - 1) / 26;
    }
    result
}

// MARK: - Loader

pub fn load_tabular_file(path: &Path) -> Result<TabularWorkbook, String> {
    let ext = path
        .extension()
        .and_then(|e| e.to_str())
        .unwrap_or("")
        .to_lowercase();

    if ext == "csv" || ext == "tsv" || ext == "txt" {
        load_csv_file(path, ext == "tsv")
    } else {
        load_calamine_file(path)
    }
}

fn load_csv_file(path: &Path, is_tsv: bool) -> Result<TabularWorkbook, String> {
    let file = File::open(path).map_err(|e| format!("Failed to open CSV: {}", e))?;
    let mmap = unsafe { memmap2::Mmap::map(&file) }.map_err(|e| format!("Mmap failed: {}", e))?;

    let delimiter = if is_tsv { b'\t' } else { b',' };
    let mut reader = csv::ReaderBuilder::new()
        .has_headers(false)
        .delimiter(delimiter)
        .flexible(true)
        .from_reader(&mmap[..]);

    let mut rows: Vec<TabularRow> = Vec::new();
    let mut headers: Vec<String> = Vec::new();
    let mut max_cols = 0;

    let mut record = csv::ByteRecord::new();
    let mut row_idx = 0;

    while reader.read_byte_record(&mut record).map_err(|e| format!("CSV read error: {}", e))? {
        if record.is_empty() {
            continue;
        }

        let mut cells = Vec::with_capacity(record.len());
        let mut hasher = AHasher::default();

        for (col_idx, field_bytes) in record.iter().enumerate() {
            let field_str = String::from_utf8_lossy(field_bytes).to_string();
            field_str.hash(&mut hasher);

            let (cell_type, formatted) = parse_string_cell(&field_str);

            cells.push(ExcelCellData {
                column_index: col_idx,
                column_letter: column_letter(col_idx),
                raw_value: field_str,
                formatted_value: formatted,
                formula: None,
                cell_type,
            });
        }

        let row_hash = hasher.finish();
        max_cols = max_cols.max(cells.len());

        if row_idx == 0 {
            headers = cells.iter().map(|c| c.raw_value.clone()).collect();
        }

        rows.push(TabularRow { cells, row_hash });
        row_idx += 1;
    }

    let file_name = path
        .file_stem()
        .and_then(|s| s.to_str())
        .unwrap_or("Sheet1")
        .to_string();

    Ok(TabularWorkbook {
        sheets: vec![TabularSheet {
            name: file_name,
            headers,
            rows,
            max_cols,
        }],
    })
}

fn load_calamine_file(path: &Path) -> Result<TabularWorkbook, String> {
    let mut workbook = open_workbook_auto(path).map_err(|e| format!("Failed to open Excel workbook: {}", e))?;
    let sheet_names = workbook.sheet_names().to_vec();

    let mut sheets = Vec::new();

    for name in sheet_names {
        if let Ok(range) = workbook.worksheet_range(&name) {
            let mut rows = Vec::with_capacity(range.height());
            let mut headers = Vec::new();
            let max_cols = range.width();

            for (r_idx, row) in range.rows().enumerate() {
                let mut cells = Vec::with_capacity(row.len());
                let mut hasher = AHasher::default();

                for (c_idx, cell) in row.iter().enumerate() {
                    let (raw_value, formatted_value, cell_type) = match cell {
                        Data::Empty => (String::new(), String::new(), ExcelCellType::Blank),
                        Data::String(s) => (s.clone(), s.clone(), ExcelCellType::String),
                        Data::Float(f) => (f.to_string(), format_float(*f), ExcelCellType::Number),
                        Data::Int(i) => (i.to_string(), i.to_string(), ExcelCellType::Number),
                        Data::Bool(b) => (b.to_string().to_uppercase(), b.to_string().to_uppercase(), ExcelCellType::Boolean),
                        Data::DateTime(d) => (d.to_string(), format_excel_date(d.as_f64()), ExcelCellType::Date),
                        Data::DateTimeIso(iso) => (iso.clone(), iso.clone(), ExcelCellType::Date),
                        Data::DurationIso(dur) => (dur.clone(), dur.clone(), ExcelCellType::Date),
                        Data::Error(err) => (format!("{:?}", err), format!("{:?}", err), ExcelCellType::Error),
                    };

                    raw_value.hash(&mut hasher);

                    cells.push(ExcelCellData {
                        column_index: c_idx,
                        column_letter: column_letter(c_idx),
                        raw_value,
                        formatted_value,
                        formula: None,
                        cell_type,
                    });
                }

                if r_idx == 0 {
                    headers = cells.iter().map(|c| c.raw_value.clone()).collect();
                }

                let row_hash = hasher.finish();
                rows.push(TabularRow { cells, row_hash });
            }

            sheets.push(TabularSheet {
                name,
                headers,
                rows,
                max_cols,
            });
        }
    }

    Ok(TabularWorkbook { sheets })
}

fn parse_string_cell(s: &str) -> (ExcelCellType, String) {
    if s.is_empty() {
        return (ExcelCellType::Blank, String::new());
    }
    if let Ok(f) = s.parse::<f64>() {
        return (ExcelCellType::Number, format_float(f));
    }
    if s.eq_ignore_ascii_case("true") || s.eq_ignore_ascii_case("false") {
        return (ExcelCellType::Boolean, s.to_uppercase());
    }
    (ExcelCellType::String, s.to_string())
}

fn format_float(f: f64) -> String {
    if (f.fract()).abs() < 1e-9 {
        format!("{:.0}", f)
    } else {
        format!("{:.4}", f).trim_end_matches('0').trim_end_matches('.').to_string()
    }
}

fn format_excel_date(serial: f64) -> String {
    let days = serial.floor() as i64;
    let seconds_in_day = ((serial - serial.floor()) * 86400.0).round() as i64;
    format!("Date({}d, {}s)", days, seconds_in_day)
}

// MARK: - Diff Engine

pub struct SheetComparisonData {
    pub summary: ExcelSheetDiffSummary,
    pub aligned_rows: Vec<ExcelRowDiffResult>,
}

pub struct ExcelDiffSession {
    pub sheets: Vec<SheetComparisonData>,
    pub total_differences: usize,
    pub duration_ms: u64,
}

impl ExcelDiffSession {
    pub fn new(left_path: &Path, right_path: &Path, rules: &ExcelCompareRules) -> Result<Self, String> {
        let start = std::time::Instant::now();

        let left_wb = load_tabular_file(left_path)?;
        let right_wb = load_tabular_file(right_path)?;

        let mut sheets = Vec::new();
        let mut total_differences = 0;

        let max_sheets = left_wb.sheets.len().max(right_wb.sheets.len());

        for i in 0..max_sheets {
            let left_sheet = left_wb.sheets.get(i);
            let right_sheet = right_wb.sheets.get(i);

            match (left_sheet, right_sheet) {
                (Some(l), Some(r)) => {
                    let cmp = compare_single_sheet(l, r, rules);
                    total_differences += cmp.summary.difference_row_count;
                    sheets.push(cmp);
                }
                (Some(l), None) => {
                    let cmp = make_single_side_sheet(l, true);
                    total_differences += cmp.summary.difference_row_count;
                    sheets.push(cmp);
                }
                (None, Some(r)) => {
                    let cmp = make_single_side_sheet(r, false);
                    total_differences += cmp.summary.difference_row_count;
                    sheets.push(cmp);
                }
                (None, None) => {}
            }
        }

        let duration_ms = start.elapsed().as_millis() as u64;

        Ok(Self {
            sheets,
            total_differences,
            duration_ms,
        })
    }

    pub fn get_summary(&self) -> ExcelWorkbookDiffSummary {
        ExcelWorkbookDiffSummary {
            sheet_summaries: self.sheets.iter().map(|s| s.summary.clone()).collect(),
            total_differences: self.total_differences,
            duration_ms: self.duration_ms,
        }
    }

    pub fn get_viewport(&self, sheet_idx: usize, start_row: usize, count: usize) -> Vec<ExcelRowDiffResult> {
        if let Some(sheet) = self.sheets.get(sheet_idx) {
            let end_row = (start_row + count).min(sheet.aligned_rows.len());
            if start_row < end_row {
                return sheet.aligned_rows[start_row..end_row].to_vec();
            }
        }
        vec![]
    }

    pub fn find_next_diff_row(&self, sheet_idx: usize, current_row: usize, backwards: bool) -> Option<usize> {
        let sheet = self.sheets.get(sheet_idx)?;
        let indices = &sheet.summary.diff_row_indices;

        if backwards {
            indices.iter().rev().copied().find(|&idx| idx < current_row)
        } else {
            indices.iter().copied().find(|&idx| idx > current_row)
        }
    }
}

fn compare_single_sheet(left: &TabularSheet, right: &TabularSheet, rules: &ExcelCompareRules) -> SheetComparisonData {
    let max_cols = left.max_cols.max(right.max_cols);
    let mut aligned_rows = Vec::new();
    let mut diff_row_indices = Vec::new();

    let mut equal_count = 0;
    let mut diff_count = 0;
    let mut left_only_count = 0;
    let mut right_only_count = 0;

    // Check if key columns matching is requested
    let key_indices: Vec<usize> = rules
        .key_columns
        .iter()
        .filter_map(|col_name| {
            left.headers
                .iter()
                .position(|h| h.eq_ignore_ascii_case(col_name))
        })
        .collect();

    if !key_indices.is_empty() {
        // Hash Join on Key Columns
        let mut right_key_map: HashMap<String, (usize, &TabularRow)> = HashMap::new();
        for (r_idx, r_row) in right.rows.iter().enumerate() {
            let key = extract_key(r_row, &key_indices, rules);
            right_key_map.insert(key, (r_idx, r_row));
        }

        let mut matched_right_indices = HashSet::new();

        for (l_idx, l_row) in left.rows.iter().enumerate() {
            let key = extract_key(l_row, &key_indices, rules);
            if let Some(&(r_idx, r_row)) = right_key_map.get(&key) {
                matched_right_indices.insert(r_idx);
                let row_diff = compare_row_cells(Some(l_idx + 1), Some(r_idx + 1), l_row, r_row, max_cols, rules);
                if row_diff.has_diff {
                    diff_count += 1;
                    diff_row_indices.push(aligned_rows.len());
                } else {
                    equal_count += 1;
                }
                aligned_rows.push(row_diff);
            } else {
                left_only_count += 1;
                diff_count += 1;
                diff_row_indices.push(aligned_rows.len());
                aligned_rows.push(make_single_row_diff(Some(l_idx + 1), None, l_row, RowDiffStatus::LeftOnly));
            }
        }

        for (r_idx, r_row) in right.rows.iter().enumerate() {
            if !matched_right_indices.contains(&r_idx) {
                right_only_count += 1;
                diff_count += 1;
                diff_row_indices.push(aligned_rows.len());
                aligned_rows.push(make_single_row_diff(None, Some(r_idx + 1), r_row, RowDiffStatus::RightOnly));
            }
        }
    } else {
        // Parallel Row Hash & Index Comparison
        let max_rows = left.rows.len().max(right.rows.len());

        for i in 0..max_rows {
            let l_opt = left.rows.get(i);
            let r_opt = right.rows.get(i);

            match (l_opt, r_opt) {
                (Some(l), Some(r)) => {
                    if l.row_hash == r.row_hash {
                        equal_count += 1;
                        aligned_rows.push(ExcelRowDiffResult {
                            row_index_left: Some(i + 1),
                            row_index_right: Some(i + 1),
                            status: RowDiffStatus::Equal,
                            cell_diffs: vec![],
                            has_diff: false,
                        });
                    } else {
                        let row_diff = compare_row_cells(Some(i + 1), Some(i + 1), l, r, max_cols, rules);
                        if row_diff.has_diff {
                            diff_count += 1;
                            diff_row_indices.push(aligned_rows.len());
                        } else {
                            equal_count += 1;
                        }
                        aligned_rows.push(row_diff);
                    }
                }
                (Some(l), None) => {
                    left_only_count += 1;
                    diff_count += 1;
                    diff_row_indices.push(aligned_rows.len());
                    aligned_rows.push(make_single_row_diff(Some(i + 1), None, l, RowDiffStatus::LeftOnly));
                }
                (None, Some(r)) => {
                    right_only_count += 1;
                    diff_count += 1;
                    diff_row_indices.push(aligned_rows.len());
                    aligned_rows.push(make_single_row_diff(None, Some(i + 1), r, RowDiffStatus::RightOnly));
                }
                (None, None) => break,
            }
        }
    }

    let summary = ExcelSheetDiffSummary {
        sheet_name_left: left.name.clone(),
        sheet_name_right: right.name.clone(),
        total_rows: aligned_rows.len(),
        total_columns: max_cols,
        difference_row_count: diff_count,
        equal_row_count: equal_count,
        left_only_row_count: left_only_count,
        right_only_row_count: right_only_count,
        diff_row_indices,
    };

    SheetComparisonData {
        summary,
        aligned_rows,
    }
}

fn compare_row_cells(
    l_idx: Option<usize>,
    r_idx: Option<usize>,
    l_row: &TabularRow,
    r_row: &TabularRow,
    max_cols: usize,
    rules: &ExcelCompareRules,
) -> ExcelRowDiffResult {
    let mut cell_diffs = Vec::new();
    let mut has_diff = false;

    for col in 0..max_cols {
        let l_cell = l_row.cells.get(col);
        let r_cell = r_row.cells.get(col);

        match (l_cell, r_cell) {
            (Some(lc), Some(rc)) => {
                let is_equal = check_cell_equal(lc, rc, rules);
                if !is_equal {
                    has_diff = true;
                    cell_diffs.push(CellDiffDetail {
                        column_index: col,
                        column_letter: column_letter(col),
                        left_cell: Some(lc.clone()),
                        right_cell: Some(rc.clone()),
                        status: CellDiffStatus::Modified,
                        diff_reason: Some(format!("'{}' -> '{}'", lc.formatted_value, rc.formatted_value)),
                    });
                }
            }
            (Some(lc), None) => {
                if !lc.raw_value.is_empty() {
                    has_diff = true;
                    cell_diffs.push(CellDiffDetail {
                        column_index: col,
                        column_letter: column_letter(col),
                        left_cell: Some(lc.clone()),
                        right_cell: None,
                        status: CellDiffStatus::Deleted,
                        diff_reason: Some("Left only".to_string()),
                    });
                }
            }
            (None, Some(rc)) => {
                if !rc.raw_value.is_empty() {
                    has_diff = true;
                    cell_diffs.push(CellDiffDetail {
                        column_index: col,
                        column_letter: column_letter(col),
                        left_cell: None,
                        right_cell: Some(rc.clone()),
                        status: CellDiffStatus::Added,
                        diff_reason: Some("Right only".to_string()),
                    });
                }
            }
            (None, None) => {}
        }
    }

    ExcelRowDiffResult {
        row_index_left: l_idx,
        row_index_right: r_idx,
        status: if has_diff { RowDiffStatus::Modified } else { RowDiffStatus::Equal },
        cell_diffs,
        has_diff,
    }
}

fn check_cell_equal(lc: &ExcelCellData, rc: &ExcelCellData, rules: &ExcelCompareRules) -> bool {
    if lc.cell_type == ExcelCellType::Number && rc.cell_type == ExcelCellType::Number {
        if let (Ok(f1), Ok(f2)) = (lc.raw_value.parse::<f64>(), rc.raw_value.parse::<f64>()) {
            return (f1 - f2).abs() <= rules.numeric_tolerance;
        }
    }

    let mut s1 = lc.raw_value.as_str();
    let mut s2 = rc.raw_value.as_str();

    if rules.ignore_whitespace {
        s1 = s1.trim();
        s2 = s2.trim();
    }

    if rules.ignore_case {
        s1.eq_ignore_ascii_case(s2)
    } else {
        s1 == s2
    }
}

fn extract_key(row: &TabularRow, key_indices: &[usize], rules: &ExcelCompareRules) -> String {
    let mut parts = Vec::new();
    for &idx in key_indices {
        if let Some(cell) = row.cells.get(idx) {
            let mut val = cell.raw_value.clone();
            if rules.ignore_whitespace {
                val = val.trim().to_string();
            }
            if rules.ignore_case {
                val = val.to_lowercase();
            }
            parts.push(val);
        } else {
            parts.push(String::new());
        }
    }
    parts.join("###")
}

fn make_single_row_diff(
    l_idx: Option<usize>,
    r_idx: Option<usize>,
    row: &TabularRow,
    status: RowDiffStatus,
) -> ExcelRowDiffResult {
    let mut cell_diffs = Vec::new();
    for (col, cell) in row.cells.iter().enumerate() {
        if !cell.raw_value.is_empty() {
            cell_diffs.push(CellDiffDetail {
                column_index: col,
                column_letter: column_letter(col),
                left_cell: if status == RowDiffStatus::LeftOnly { Some(cell.clone()) } else { None },
                right_cell: if status == RowDiffStatus::RightOnly { Some(cell.clone()) } else { None },
                status: if status == RowDiffStatus::LeftOnly { CellDiffStatus::Deleted } else { CellDiffStatus::Added },
                diff_reason: None,
            });
        }
    }

    ExcelRowDiffResult {
        row_index_left: l_idx,
        row_index_right: r_idx,
        status,
        cell_diffs,
        has_diff: true,
    }
}

fn make_single_side_sheet(sheet: &TabularSheet, is_left: bool) -> SheetComparisonData {
    let mut aligned_rows = Vec::new();
    let mut diff_row_indices = Vec::new();

    for (idx, row) in sheet.rows.iter().enumerate() {
        diff_row_indices.push(aligned_rows.len());
        let status = if is_left { RowDiffStatus::LeftOnly } else { RowDiffStatus::RightOnly };
        aligned_rows.push(make_single_row_diff(
            if is_left { Some(idx + 1) } else { None },
            if !is_left { Some(idx + 1) } else { None },
            row,
            status,
        ));
    }

    let summary = ExcelSheetDiffSummary {
        sheet_name_left: if is_left { sheet.name.clone() } else { String::new() },
        sheet_name_right: if !is_left { sheet.name.clone() } else { String::new() },
        total_rows: aligned_rows.len(),
        total_columns: sheet.max_cols,
        difference_row_count: aligned_rows.len(),
        equal_row_count: 0,
        left_only_row_count: if is_left { aligned_rows.len() } else { 0 },
        right_only_row_count: if !is_left { aligned_rows.len() } else { 0 },
        diff_row_indices,
    };

    SheetComparisonData {
        summary,
        aligned_rows,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_column_letter() {
        assert_eq!(column_letter(0), "A");
        assert_eq!(column_letter(1), "B");
        assert_eq!(column_letter(25), "Z");
        assert_eq!(column_letter(26), "AA");
        assert_eq!(column_letter(27), "AB");
    }

    #[test]
    fn test_csv_comparison() {
        use std::io::Write;
        let dir = std::env::temp_dir();
        let path_a = dir.join("test_a.csv");
        let path_b = dir.join("test_b.csv");

        let mut file_a = File::create(&path_a).unwrap();
        writeln!(file_a, "ID,Name,Amount\n1,Alice,100.5\n2,Bob,200.0").unwrap();

        let mut file_b = File::create(&path_b).unwrap();
        writeln!(file_b, "ID,Name,Amount\n1,Alice,100.5001\n2,Bob,250.0\n3,Charlie,300.0").unwrap();

        let rules = ExcelCompareRules {
            numeric_tolerance: 0.01,
            ..Default::default()
        };

        let session = ExcelDiffSession::new(&path_a, &path_b, &rules).expect("Session creation failed");
        let summary = session.get_summary();
        assert_eq!(summary.total_differences, 2); // Row 2 (modified) + Row 3 (right only)

        let viewport = session.get_viewport(0, 0, 10);
        assert_eq!(viewport.len(), 4); // header + 3 rows
    }
}
