//! MacCompare Core Diff Engine
//! Two-stage diff calculation: line-level Hunk matching + token/character-level fine diff.

use serde::{Deserialize, Serialize};

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
}

/// Overall 3-way merge result.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, Default)]
pub struct MergeResult {
    pub lines: Vec<MergeLine>,
    pub conflict_count: u32,
    pub auto_resolved_count: u32,
    pub merged_text: String,
}

/// Primary 2-way text comparison function.
pub fn compare_text(
    left_content: &str,
    right_content: &str,
    options: &DiffOptions,
) -> TextDiffResult {
    let mut result = TextDiffResult::default();

    let left_lines: Vec<&str> = left_content.lines().collect();
    let right_lines: Vec<&str> = right_content.lines().collect();

    let max_len = left_lines.len().max(right_lines.len());
    let mut l_idx = 0;
    let mut r_idx = 0;

    // Simple baseline Myers-like aligner for core scaffolding
    while l_idx < left_lines.len() || r_idx < right_lines.len() {
        let left_line = left_lines.get(l_idx).copied();
        let right_line = right_lines.get(r_idx).copied();

        match (left_line, right_line) {
            (Some(l), Some(r)) => {
                let is_match = if options.ignore_whitespace {
                    l.trim() == r.trim()
                } else if options.ignore_case {
                    l.to_lowercase() == r.to_lowercase()
                } else {
                    l == r
                };

                if is_match {
                    result.lines.push(DiffLine {
                        left_line_number: Some((l_idx + 1) as u32),
                        right_line_number: Some((r_idx + 1) as u32),
                        content_left: l.to_string(),
                        content_right: r.to_string(),
                        change_type: ChangeType::Unchanged,
                        tokens_left: vec![],
                        tokens_right: vec![],
                    });
                    l_idx += 1;
                    r_idx += 1;
                } else {
                    // Line modified or added/deleted
                    let tokens_left = compute_token_diff(l, r, ChangeType::Deleted);
                    let tokens_right = compute_token_diff(r, l, ChangeType::Added);

                    result.lines.push(DiffLine {
                        left_line_number: Some((l_idx + 1) as u32),
                        right_line_number: Some((r_idx + 1) as u32),
                        content_left: l.to_string(),
                        content_right: r.to_string(),
                        change_type: ChangeType::Modified,
                        tokens_left,
                        tokens_right,
                    });
                    result.total_modifications += 1;
                    l_idx += 1;
                    r_idx += 1;
                }
            }
            (Some(l), None) => {
                // Deleted on right (Phantom on right)
                result.lines.push(DiffLine {
                    left_line_number: Some((l_idx + 1) as u32),
                    right_line_number: None,
                    content_left: l.to_string(),
                    content_right: String::new(),
                    change_type: ChangeType::Deleted,
                    tokens_left: vec![DiffToken {
                        start_offset: 0,
                        length: l.len() as u32,
                        change_type: ChangeType::Deleted,
                    }],
                    tokens_right: vec![],
                });
                result.total_deletions += 1;
                l_idx += 1;
            }
            (None, Some(r)) => {
                // Added on right (Phantom on left)
                result.lines.push(DiffLine {
                    left_line_number: None,
                    right_line_number: Some((r_idx + 1) as u32),
                    content_left: String::new(),
                    content_right: r.to_string(),
                    change_type: ChangeType::Added,
                    tokens_left: vec![],
                    tokens_right: vec![DiffToken {
                        start_offset: 0,
                        length: r.len() as u32,
                        change_type: ChangeType::Added,
                    }],
                });
                result.total_additions += 1;
                r_idx += 1;
            }
            (None, None) => break,
        }
    }

    result
}

/// Compute character / token differences between two lines
fn compute_token_diff(source: &str, target: &str, change_type: ChangeType) -> Vec<DiffToken> {
    if source == target {
        return vec![];
    }
    // Tokenize words
    let mut tokens = Vec::new();
    let mut offset = 0;
    for word in source.split_whitespace() {
        if !target.contains(word) {
            tokens.push(DiffToken {
                start_offset: offset as u32,
                length: word.len() as u32,
                change_type,
            });
        }
        offset += word.len() + 1;
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

        let status = match (loc_changed, rem_changed) {
            (false, false) => {
                merged_output.push(bas.to_string());
                MergeHunkStatus::Unchanged
            }
            (true, false) => {
                merged_output.push(loc.to_string());
                result.auto_resolved_count += 1;
                MergeHunkStatus::CleanLocal
            }
            (false, true) => {
                merged_output.push(rem.to_string());
                result.auto_resolved_count += 1;
                MergeHunkStatus::CleanRemote
            }
            (true, true) => {
                if loc == rem {
                    merged_output.push(loc.to_string());
                    result.auto_resolved_count += 1;
                    MergeHunkStatus::Unchanged
                } else {
                    result.conflict_count += 1;
                    merged_output.push(format!("<<<<<<< Local\n{}\n=======\n{}\n>>>>>>> Remote", loc, rem));
                    MergeHunkStatus::Conflict
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
        });
    }

    result.merged_text = merged_output.join("\n");
    result
}
