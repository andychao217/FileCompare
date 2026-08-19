//! Core Diff Integration Tests

use diff_core::{compare_text, merge_three_way, ChangeType, DiffOptions, MergeHunkStatus};

#[test]
fn test_basic_line_diff() {
    let left = "line 1\nline 2\nline 3";
    let right = "line 1\nline 2 modified\nline 3";

    let result = compare_text(left, right, &DiffOptions::default());
    assert_eq!(result.lines.len(), 3);
    assert_eq!(result.lines[0].change_type, ChangeType::Unchanged);
    assert_eq!(result.lines[1].change_type, ChangeType::Modified);
    assert_eq!(result.lines[2].change_type, ChangeType::Unchanged);
}

#[test]
fn test_three_way_merge_clean() {
    let base = "line 1\nline 2\nline 3";
    let local = "line 1\nline 2 local\nline 3";
    let remote = "line 1\nline 2\nline 3";

    let result = merge_three_way(local, base, remote);
    assert_eq!(result.conflict_count, 0);
    assert_eq!(result.lines[1].status, MergeHunkStatus::CleanLocal);
}

#[test]
fn test_three_way_merge_conflict() {
    let base = "line 1\nline 2\nline 3";
    let local = "line 1\nline 2 local\nline 3";
    let remote = "line 1\nline 2 remote\nline 3";

    let result = merge_three_way(local, base, remote);
    assert_eq!(result.conflict_count, 1);
    assert_eq!(result.lines[1].status, MergeHunkStatus::Conflict);
}
