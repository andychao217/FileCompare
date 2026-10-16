//! MacCompare Core Diff Engine
//! Two-stage diff calculation: line-level Hunk matching + token/character-level fine diff.

use serde::{Deserialize, Serialize};
use similar::{Algorithm, ChangeTag, DiffOp, TextDiff};

/// Type of change for line or token diff.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum ChangeType {
    Unchanged,
    Added,
    Deleted,
    Modified,
}

/// Token or character-level fine difference within a line.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct DiffToken {
    pub start_offset: u32,
    pub length: u32,
    pub change_type: ChangeType,
}

/// A contiguous group of changed lines (Hunk).
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct DiffHunk {
    pub id: usize,
    pub start_line_index: usize,
    pub line_count: usize,
    pub change_type: ChangeType,
}

/// A visual diff line in a 2-way diff view (supports Phantom line alignment).
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct DiffLine {
    pub left_line_number: Option<u32>,
    pub right_line_number: Option<u32>,
    pub content_left: String,
    pub content_right: String,
    pub change_type: ChangeType,
    pub tokens_left: Vec<DiffToken>,
    pub tokens_right: Vec<DiffToken>,
    pub hunk_index: Option<usize>,
}

/// Diff algorithm and normalization options.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, Default)]
pub struct DiffOptions {
    pub ignore_whitespace: bool,
    pub ignore_case: bool,
    pub ignore_line_endings: bool,
    pub ignore_comments: bool,
}

/// Overall 2-way text diff result summary.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, Default)]
pub struct TextDiffResult {
    pub lines: Vec<DiffLine>,
    pub total_additions: u32,
    pub total_deletions: u32,
    pub total_modifications: u32,
    pub hunks: Vec<DiffHunk>,
}

/// 3-Way Merge conflict hunk status.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum MergeHunkStatus {
    CleanLocal,
    CleanRemote,
    Conflict,
    Unchanged,
}

/// A line in a 3-way merge view.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct MergeLine {
    pub local_line_number: Option<u32>,
    pub base_line_number: Option<u32>,
    pub remote_line_number: Option<u32>,
    pub content_local: String,
    pub content_base: String,
    pub content_remote: String,
    pub status: MergeHunkStatus,
    pub resolved_content: Option<String>,
    pub conflict_index: Option<usize>,
}

/// Overall 3-way merge result.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, Default)]
pub struct MergeResult {
    pub lines: Vec<MergeLine>,
    pub conflict_count: u32,
    pub auto_resolved_count: u32,
    pub merged_text: String,
}

/// Normalize line according to DiffOptions.
fn normalize_line<'a>(line: &'a str, options: &DiffOptions) -> std::borrow::Cow<'a, str> {
    if options.ignore_whitespace && options.ignore_case {
        std::borrow::Cow::Owned(line.trim().to_lowercase())
    } else if options.ignore_whitespace {
        std::borrow::Cow::Borrowed(line.trim())
    } else if options.ignore_case {
        std::borrow::Cow::Owned(line.to_lowercase())
    } else {
        std::borrow::Cow::Borrowed(line)
    }
}

/// Primary 2-way text comparison function utilizing the Myers diff algorithm.
pub fn compare_text(
    left_content: &str,
    right_content: &str,
    options: &DiffOptions,
) -> TextDiffResult {
    let left_normalized;
    let right_normalized;
    let (left_str, right_str) = if left_content.contains('\r') || right_content.contains('\r') {
        left_normalized = left_content.replace("\r\n", "\n").replace('\r', "\n");
        right_normalized = right_content.replace("\r\n", "\n").replace('\r', "\n");
        (left_normalized.as_str(), right_normalized.as_str())
    } else {
        (left_content, right_content)
    };

    let left_raw_lines: Vec<&str> = left_str.lines().collect();
    let right_raw_lines: Vec<&str> = right_str.lines().collect();

    let left_norm: Vec<String> = left_raw_lines
        .iter()
        .map(|l| normalize_line(l, options).into_owned())
        .collect();
    let right_norm: Vec<String> = right_raw_lines
        .iter()
        .map(|r| normalize_line(r, options).into_owned())
        .collect();

    let left_slices: Vec<&str> = left_norm.iter().map(|s| s.as_str()).collect();
    let right_slices: Vec<&str> = right_norm.iter().map(|s| s.as_str()).collect();

    let diff = TextDiff::configure()
        .algorithm(Algorithm::Myers)
        .diff_slices(&left_slices, &right_slices);

    let mut lines: Vec<DiffLine> = Vec::new();
    let mut total_additions: u32 = 0;
    let mut total_deletions: u32 = 0;
    let mut total_modifications: u32 = 0;
    let mut hunks: Vec<DiffHunk> = Vec::new();

    let mut current_hunk_start: Option<usize> = None;
    let mut current_hunk_type = ChangeType::Unchanged;
    let mut current_hunk_count = 0;

    for op in diff.ops() {
        match *op {
            DiffOp::Equal {
                old_index,
                new_index,
                len,
            } => {
                if let Some(start) = current_hunk_start.take() {
                    hunks.push(DiffHunk {
                        id: hunks.len(),
                        start_line_index: start,
                        line_count: current_hunk_count,
                        change_type: current_hunk_type,
                    });
                    current_hunk_count = 0;
                }

                for i in 0..len {
                    let l_idx = old_index + i;
                    let r_idx = new_index + i;
                    lines.push(DiffLine {
                        left_line_number: Some((l_idx + 1) as u32),
                        right_line_number: Some((r_idx + 1) as u32),
                        content_left: left_raw_lines.get(l_idx).copied().unwrap_or("").to_string(),
                        content_right: right_raw_lines.get(r_idx).copied().unwrap_or("").to_string(),
                        change_type: ChangeType::Unchanged,
                        tokens_left: vec![],
                        tokens_right: vec![],
                        hunk_index: None,
                    });
                }
            }
            DiffOp::Replace {
                old_index,
                old_len,
                new_index,
                new_len,
            } => {
                let hunk_id = hunks.len();
                if current_hunk_start.is_none() {
                    current_hunk_start = Some(lines.len());
                    current_hunk_type = ChangeType::Modified;
                }

                let max_len = old_len.max(new_len);
                for i in 0..max_len {
                    let has_left = i < old_len;
                    let has_right = i < new_len;

                    let l_idx = if has_left { Some(old_index + i) } else { None };
                    let r_idx = if has_right { Some(new_index + i) } else { None };

                    let l_raw = l_idx.and_then(|idx| left_raw_lines.get(idx).copied()).unwrap_or("");
                    let r_raw = r_idx.and_then(|idx| right_raw_lines.get(idx).copied()).unwrap_or("");

                    if has_left && has_right {
                        total_modifications += 1;
                        let tokens_left = compute_token_diff(l_raw, r_raw, ChangeType::Deleted);
                        let tokens_right = compute_token_diff(r_raw, l_raw, ChangeType::Added);

                        lines.push(DiffLine {
                            left_line_number: l_idx.map(|idx| (idx + 1) as u32),
                            right_line_number: r_idx.map(|idx| (idx + 1) as u32),
                            content_left: l_raw.to_string(),
                            content_right: r_raw.to_string(),
                            change_type: ChangeType::Modified,
                            tokens_left,
                            tokens_right,
                            hunk_index: Some(hunk_id),
                        });
                    } else if has_left {
                        total_deletions += 1;
                        lines.push(DiffLine {
                            left_line_number: l_idx.map(|idx| (idx + 1) as u32),
                            right_line_number: None,
                            content_left: l_raw.to_string(),
                            content_right: String::new(),
                            change_type: ChangeType::Deleted,
                            tokens_left: vec![DiffToken {
                                start_offset: 0,
                                length: l_raw.chars().count() as u32,
                                change_type: ChangeType::Deleted,
                            }],
                            tokens_right: vec![],
                            hunk_index: Some(hunk_id),
                        });
                    } else {
                        total_additions += 1;
                        lines.push(DiffLine {
                            left_line_number: None,
                            right_line_number: r_idx.map(|idx| (idx + 1) as u32),
                            content_left: String::new(),
                            content_right: r_raw.to_string(),
                            change_type: ChangeType::Added,
                            tokens_left: vec![],
                            tokens_right: vec![DiffToken {
                                start_offset: 0,
                                length: r_raw.chars().count() as u32,
                                change_type: ChangeType::Added,
                            }],
                            hunk_index: Some(hunk_id),
                        });
                    }
                    current_hunk_count += 1;
                }
            }
            DiffOp::Delete {
                old_index,
                old_len,
                ..
            } => {
                let hunk_id = hunks.len();
                if current_hunk_start.is_none() {
                    current_hunk_start = Some(lines.len());
                    current_hunk_type = ChangeType::Deleted;
                }

                for i in 0..old_len {
                    let l_idx = old_index + i;
                    let l_raw = left_raw_lines.get(l_idx).copied().unwrap_or("");
                    total_deletions += 1;
                    lines.push(DiffLine {
                        left_line_number: Some((l_idx + 1) as u32),
                        right_line_number: None,
                        content_left: l_raw.to_string(),
                        content_right: String::new(),
                        change_type: ChangeType::Deleted,
                        tokens_left: vec![DiffToken {
                            start_offset: 0,
                            length: l_raw.chars().count() as u32,
                            change_type: ChangeType::Deleted,
                        }],
                        tokens_right: vec![],
                        hunk_index: Some(hunk_id),
                    });
                    current_hunk_count += 1;
                }
            }
            DiffOp::Insert {
                new_index,
                new_len,
                ..
            } => {
                let hunk_id = hunks.len();
                if current_hunk_start.is_none() {
                    current_hunk_start = Some(lines.len());
                    current_hunk_type = ChangeType::Added;
                }

                for i in 0..new_len {
                    let r_idx = new_index + i;
                    let r_raw = right_raw_lines.get(r_idx).copied().unwrap_or("");
                    total_additions += 1;
                    lines.push(DiffLine {
                        left_line_number: None,
                        right_line_number: Some((r_idx + 1) as u32),
                        content_left: String::new(),
                        content_right: r_raw.to_string(),
                        change_type: ChangeType::Added,
                        tokens_left: vec![],
                        tokens_right: vec![DiffToken {
                            start_offset: 0,
                            length: r_raw.chars().count() as u32,
                            change_type: ChangeType::Added,
                        }],
                        hunk_index: Some(hunk_id),
                    });
                    current_hunk_count += 1;
                }
            }
        }
    }

    if let Some(start) = current_hunk_start.take() {
        hunks.push(DiffHunk {
            id: hunks.len(),
            start_line_index: start,
            line_count: current_hunk_count,
            change_type: current_hunk_type,
        });
    }

    TextDiffResult {
        lines,
        total_additions,
        total_deletions,
        total_modifications,
        hunks,
    }
}

/// Compute character / token differences between two lines
fn compute_token_diff(source: &str, target: &str, change_type: ChangeType) -> Vec<DiffToken> {
    if source == target {
        return vec![];
    }

    let diff = TextDiff::configure()
        .algorithm(Algorithm::Myers)
        .diff_words(source, target);

    let mut tokens = Vec::new();
    let mut offset: u32 = 0;

    for change in diff.iter_all_changes() {
        let text = change.value();
        let len = text.chars().count() as u32;

        match change.tag() {
            ChangeTag::Equal => {
                offset += len;
            }
            ChangeTag::Delete => {
                if change_type == ChangeType::Deleted {
                    tokens.push(DiffToken {
                        start_offset: offset,
                        length: len,
                        change_type,
                    });
                }
                offset += len;
            }
            ChangeTag::Insert => {
                if change_type == ChangeType::Added {
                    tokens.push(DiffToken {
                        start_offset: offset,
                        length: len,
                        change_type,
                    });
                }
                offset += len;
            }
        }
    }

    tokens
}

/// 3-Way Merge algorithm between Local, Base, and Remote.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum BaseLineAction {
    Kept(usize),
    Modified(usize),
    Deleted,
}

fn align_branch_to_base(base_lines: &[&str], branch_lines: &[&str]) -> (Vec<Vec<usize>>, Vec<BaseLineAction>) {
    let n = base_lines.len();
    let mut insertions: Vec<Vec<usize>> = vec![Vec::new(); n + 1];
    let mut actions: Vec<BaseLineAction> = vec![BaseLineAction::Deleted; n];

    let diff = TextDiff::configure()
        .algorithm(Algorithm::Myers)
        .diff_slices(base_lines, branch_lines);

    for op in diff.ops() {
        match *op {
            DiffOp::Equal { old_index, new_index, len } => {
                for i in 0..len {
                    actions[old_index + i] = BaseLineAction::Kept(new_index + i);
                }
            }
            DiffOp::Insert { old_index, new_index, new_len } => {
                for i in 0..new_len {
                    insertions[old_index].push(new_index + i);
                }
            }
            DiffOp::Delete { old_index, old_len, .. } => {
                for i in 0..old_len {
                    actions[old_index + i] = BaseLineAction::Deleted;
                }
            }
            DiffOp::Replace { old_index, old_len, new_index, new_len } => {
                let common = old_len.min(new_len);
                for i in 0..common {
                    actions[old_index + i] = BaseLineAction::Modified(new_index + i);
                }
                if new_len > old_len {
                    for i in common..new_len {
                        insertions[old_index + old_len].push(new_index + i);
                    }
                } else if old_len > new_len {
                    for i in common..old_len {
                        actions[old_index + i] = BaseLineAction::Deleted;
                    }
                }
            }
        }
    }

    (insertions, actions)
}

/// 3-Way Merge algorithm between Local, Base, and Remote using Myers-diff alignment.
pub fn merge_three_way(
    local_content: &str,
    base_content: &str,
    remote_content: &str,
) -> MergeResult {
    let loc_norm;
    let bas_norm;
    let rem_norm;
    let (loc_str, bas_str, rem_str) = if local_content.contains('\r') || base_content.contains('\r') || remote_content.contains('\r') {
        loc_norm = local_content.replace("\r\n", "\n").replace('\r', "\n");
        bas_norm = base_content.replace("\r\n", "\n").replace('\r', "\n");
        rem_norm = remote_content.replace("\r\n", "\n").replace('\r', "\n");
        (loc_norm.as_str(), bas_norm.as_str(), rem_norm.as_str())
    } else {
        (local_content, base_content, remote_content)
    };

    let local_lines: Vec<&str> = loc_str.lines().collect();
    let base_lines: Vec<&str> = bas_str.lines().collect();
    let remote_lines: Vec<&str> = rem_str.lines().collect();

    let (loc_insertions, loc_actions) = align_branch_to_base(&base_lines, &local_lines);
    let (rem_insertions, rem_actions) = align_branch_to_base(&base_lines, &remote_lines);

    let mut result = MergeResult::default();
    let mut merged_output = Vec::new();
    let n = base_lines.len();

    for b in 0..=n {
        // 1. Process insertions before base line b
        let loc_ins = &loc_insertions[b];
        let rem_ins = &rem_insertions[b];
        let max_ins = loc_ins.len().max(rem_ins.len());

        for k in 0..max_ins {
            let has_loc = k < loc_ins.len();
            let has_rem = k < rem_ins.len();
            let l_idx = if has_loc { Some(loc_ins[k]) } else { None };
            let r_idx = if has_rem { Some(rem_ins[k]) } else { None };

            let loc_line = l_idx.map(|i| local_lines[i]).unwrap_or("");
            let rem_line = r_idx.map(|i| remote_lines[i]).unwrap_or("");

            let (status, conflict_idx, resolved) = if has_loc && has_rem {
                if loc_line == rem_line {
                    result.auto_resolved_count += 1;
                    (MergeHunkStatus::CleanLocal, None, Some(loc_line.to_string()))
                } else {
                    let c_idx = result.conflict_count as usize;
                    result.conflict_count += 1;
                    let block = format!("<<<<<<< Local\n{}\n=======\n{}\n>>>>>>> Remote", loc_line, rem_line);
                    (MergeHunkStatus::Conflict, Some(c_idx), Some(block))
                }
            } else if has_loc {
                result.auto_resolved_count += 1;
                (MergeHunkStatus::CleanLocal, None, Some(loc_line.to_string()))
            } else {
                result.auto_resolved_count += 1;
                (MergeHunkStatus::CleanRemote, None, Some(rem_line.to_string()))
            };

            if let Some(ref text) = resolved {
                merged_output.push(text.clone());
            }

            result.lines.push(MergeLine {
                local_line_number: l_idx.map(|i| (i + 1) as u32),
                base_line_number: None,
                remote_line_number: r_idx.map(|i| (i + 1) as u32),
                content_local: loc_line.to_string(),
                content_base: String::new(),
                content_remote: rem_line.to_string(),
                status,
                resolved_content: resolved,
                conflict_index: conflict_idx,
            });
        }

        // 2. Process base line b (if b < n)
        if b < n {
            let bas_line = base_lines[b];
            let loc_act = loc_actions[b];
            let rem_act = rem_actions[b];

            let (l_idx, loc_line) = match loc_act {
                BaseLineAction::Kept(i) | BaseLineAction::Modified(i) => (Some(i), local_lines[i]),
                BaseLineAction::Deleted => (None, ""),
            };

            let (r_idx, rem_line) = match rem_act {
                BaseLineAction::Kept(i) | BaseLineAction::Modified(i) => (Some(i), remote_lines[i]),
                BaseLineAction::Deleted => (None, ""),
            };

            let (status, conflict_idx, resolved) = match (loc_act, rem_act) {
                (BaseLineAction::Kept(_), BaseLineAction::Kept(_)) => {
                    (MergeHunkStatus::Unchanged, None, Some(bas_line.to_string()))
                }
                (BaseLineAction::Modified(_), BaseLineAction::Kept(_)) => {
                    result.auto_resolved_count += 1;
                    (MergeHunkStatus::CleanLocal, None, Some(loc_line.to_string()))
                }
                (BaseLineAction::Kept(_), BaseLineAction::Modified(_)) => {
                    result.auto_resolved_count += 1;
                    (MergeHunkStatus::CleanRemote, None, Some(rem_line.to_string()))
                }
                (BaseLineAction::Deleted, BaseLineAction::Kept(_)) => {
                    result.auto_resolved_count += 1;
                    (MergeHunkStatus::CleanLocal, None, None)
                }
                (BaseLineAction::Kept(_), BaseLineAction::Deleted) => {
                    result.auto_resolved_count += 1;
                    (MergeHunkStatus::CleanRemote, None, None)
                }
                (BaseLineAction::Deleted, BaseLineAction::Deleted) => {
                    result.auto_resolved_count += 1;
                    (MergeHunkStatus::CleanLocal, None, None)
                }
                (BaseLineAction::Modified(_), BaseLineAction::Modified(_)) => {
                    if loc_line == rem_line {
                        result.auto_resolved_count += 1;
                        (MergeHunkStatus::CleanLocal, None, Some(loc_line.to_string()))
                    } else {
                        let c_idx = result.conflict_count as usize;
                        result.conflict_count += 1;
                        let block = format!("<<<<<<< Local\n{}\n=======\n{}\n>>>>>>> Remote", loc_line, rem_line);
                        (MergeHunkStatus::Conflict, Some(c_idx), Some(block))
                    }
                }
                (BaseLineAction::Modified(_), BaseLineAction::Deleted) => {
                    let c_idx = result.conflict_count as usize;
                    result.conflict_count += 1;
                    let block = format!("<<<<<<< Local\n{}\n=======\n\n>>>>>>> Remote", loc_line);
                    (MergeHunkStatus::Conflict, Some(c_idx), Some(block))
                }
                (BaseLineAction::Deleted, BaseLineAction::Modified(_)) => {
                    let c_idx = result.conflict_count as usize;
                    result.conflict_count += 1;
                    let block = format!("<<<<<<< Local\n\n=======\n{}\n>>>>>>> Remote", rem_line);
                    (MergeHunkStatus::Conflict, Some(c_idx), Some(block))
                }
            };

            if let Some(ref text) = resolved {
                merged_output.push(text.clone());
            }

            result.lines.push(MergeLine {
                local_line_number: l_idx.map(|i| (i + 1) as u32),
                base_line_number: Some((b + 1) as u32),
                remote_line_number: r_idx.map(|i| (i + 1) as u32),
                content_local: loc_line.to_string(),
                content_base: bas_line.to_string(),
                content_remote: rem_line.to_string(),
                status,
                resolved_content: resolved,
                conflict_index: conflict_idx,
            });
        }
    }

    result.merged_text = merged_output.join("\n");
    result
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_compare_text_myers() {
        let left = "Hello\nWorld\nRust\nEngine";
        let right = "Hello\nSwift\nRust\nEngine\nAdded";
        let options = DiffOptions::default();

        let result = compare_text(left, right, &options);
        assert_eq!(result.total_modifications, 1);
        assert_eq!(result.total_additions, 1);
        assert_eq!(result.total_deletions, 0);

        let json = serde_json::to_string(&result).expect("Serialize to JSON");
        assert!(json.contains("start_line_index"));
        assert!(json.contains("Modified"));
    }

    #[test]
    fn test_3way_merge() {
        let local = "Apple\nBanana Modified\nCherry";
        let base = "Apple\nBanana\nCherry";
        let remote = "Apple\nBanana\nCherry 2";

        let result = merge_three_way(local, base, remote);
        assert_eq!(result.conflict_count, 0);
        assert_eq!(result.auto_resolved_count, 2);
    }

    #[test]
    fn test_cr_and_crlf_line_endings() {
        let left = "Line 1\rLine 2\rLine 3 Changed\rLine 4";
        let right = "Line 1\r\nLine 2\r\nLine 3\r\nLine 4";
        let options = DiffOptions::default();

        let result = compare_text(left, right, &options);
        assert_eq!(result.lines.len(), 4);
        assert_eq!(result.total_modifications, 1);
        assert_eq!(result.lines[0].change_type, ChangeType::Unchanged);
        assert_eq!(result.lines[1].change_type, ChangeType::Unchanged);
        assert_eq!(result.lines[2].change_type, ChangeType::Modified);
        assert_eq!(result.lines[3].change_type, ChangeType::Unchanged);
    }

    #[test]
    fn test_excel_row_display_item_diff() {
        let left = std::fs::read_to_string("../../../test_files/ExcelRowDisplayItem_2.cs").unwrap();
        let right = std::fs::read_to_string("../../../test_files/ExcelRowDisplayItem.cs").unwrap();
        let options = DiffOptions::default();

        let result = compare_text(&left, &right, &options);
        println!("Rust diff lines count: {}", result.lines.len());
        println!("Rust diff line 0: {:?}", result.lines[0]);
        println!("Rust diff line 19: {:?}", result.lines[19]);
        assert_eq!(result.lines.len(), 72);
        assert_eq!(result.lines[0].change_type, ChangeType::Unchanged);
        assert_eq!(result.lines[19].change_type, ChangeType::Modified);
    }
}
