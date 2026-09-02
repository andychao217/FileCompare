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
    let left_raw_lines: Vec<&str> = left_content.lines().collect();
    let right_raw_lines: Vec<&str> = right_content.lines().collect();

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
pub fn merge_three_way(
    local_content: &str,
    base_content: &str,
    remote_content: &str,
) -> MergeResult {
    let mut result = MergeResult::default();
    let local_lines: Vec<&str> = local_content.lines().collect();
    let base_lines: Vec<&str> = base_content.lines().collect();
    let remote_lines: Vec<&str> = remote_content.lines().collect();

    let max_len = local_lines
        .len()
        .max(base_lines.len())
        .max(remote_lines.len());

    let mut merged_output = Vec::new();

    for i in 0..max_len {
        let loc = local_lines.get(i).copied().unwrap_or("");
        let bas = base_lines.get(i).copied().unwrap_or("");
        let rem = remote_lines.get(i).copied().unwrap_or("");

        let loc_changed = loc != bas;
        let rem_changed = rem != bas;

        let (status, conflict_idx) = match (loc_changed, rem_changed) {
            (false, false) => {
                merged_output.push(bas.to_string());
                (MergeHunkStatus::Unchanged, None)
            }
            (true, false) => {
                merged_output.push(loc.to_string());
                result.auto_resolved_count += 1;
                (MergeHunkStatus::CleanLocal, None)
            }
            (false, true) => {
                merged_output.push(rem.to_string());
                result.auto_resolved_count += 1;
                (MergeHunkStatus::CleanRemote, None)
            }
            (true, true) => {
                if loc == rem {
                    merged_output.push(loc.to_string());
                    result.auto_resolved_count += 1;
                    (MergeHunkStatus::Unchanged, None)
                } else {
                    let c_idx = result.conflict_count as usize;
                    result.conflict_count += 1;
                    merged_output.push(format!("<<<<<<< Local\n{}\n=======\n{}\n>>>>>>> Remote", loc, rem));
                    (MergeHunkStatus::Conflict, Some(c_idx))
                }
            }
        };

        result.lines.push(MergeLine {
            local_line_number: if i < local_lines.len() { Some((i + 1) as u32) } else { None },
            base_line_number: if i < base_lines.len() { Some((i + 1) as u32) } else { None },
            remote_line_number: if i < remote_lines.len() { Some((i + 1) as u32) } else { None },
            content_local: loc.to_string(),
            content_base: bas.to_string(),
            content_remote: rem.to_string(),
            status,
            resolved_content: Some(merged_output.last().cloned().unwrap_or_default()),
            conflict_index: conflict_idx,
        });
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
}
