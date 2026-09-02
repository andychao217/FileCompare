//! MacCompare FFI layer for Swift integration.

use diff_core::{compare_text, merge_three_way, DiffOptions};
use excel_diff_core::{ExcelCompareRules, ExcelDiffSession};
use fs_scanner::{compare_folders, FolderCompareMode, FolderDiffOptions};
use std::ffi::{CStr, CString};
use std::os::raw::c_char;
use std::path::Path;

/// Check if the Rust core engine library is loaded and operational.
#[unsafe(no_mangle)]
pub extern "C" fn maccompare_is_available() -> bool {
    true
}

/// Return version of the Rust core engine. Caller must free with `free_rust_string`.
#[unsafe(no_mangle)]
pub extern "C" fn maccompare_version() -> *mut c_char {
    let version = env!("CARGO_PKG_VERSION");
    match CString::new(format!("MacCompare Core v{}", version)) {
        Ok(c_str) => c_str.into_raw(),
        Err(_) => std::ptr::null_mut(),
    }
}

/// C-ABI wrapper for 2-way text diff returning JSON string.
/// Caller must free returned string using `free_rust_string`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn maccompare_compare_text_json(
    left_ptr: *const c_char,
    right_ptr: *const c_char,
    ignore_whitespace: bool,
    ignore_case: bool,
) -> *mut c_char {
    if left_ptr.is_null() || right_ptr.is_null() {
        return std::ptr::null_mut();
    }

    let left = match unsafe { CStr::from_ptr(left_ptr) }.to_str() {
        Ok(s) => s,
        Err(_) => return std::ptr::null_mut(),
    };
    let right = match unsafe { CStr::from_ptr(right_ptr) }.to_str() {
        Ok(s) => s,
        Err(_) => return std::ptr::null_mut(),
    };

    let options = DiffOptions {
        ignore_whitespace,
        ignore_case,
        ignore_line_endings: true,
        ignore_comments: false,
    };

    let diff_result = compare_text(left, right, &options);
    match serde_json::to_string(&diff_result) {
        Ok(json) => match CString::new(json) {
            Ok(c_str) => c_str.into_raw(),
            Err(_) => std::ptr::null_mut(),
        },
        Err(_) => std::ptr::null_mut(),
    }
}

/// C-ABI wrapper for 3-way merge returning JSON string.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn maccompare_merge_three_way_json(
    local_ptr: *const c_char,
    base_ptr: *const c_char,
    remote_ptr: *const c_char,
) -> *mut c_char {
    if local_ptr.is_null() || base_ptr.is_null() || remote_ptr.is_null() {
        return std::ptr::null_mut();
    }

    let local = match unsafe { CStr::from_ptr(local_ptr) }.to_str() {
        Ok(s) => s,
        Err(_) => return std::ptr::null_mut(),
    };
    let base = match unsafe { CStr::from_ptr(base_ptr) }.to_str() {
        Ok(s) => s,
        Err(_) => return std::ptr::null_mut(),
    };
    let remote = match unsafe { CStr::from_ptr(remote_ptr) }.to_str() {
        Ok(s) => s,
        Err(_) => return std::ptr::null_mut(),
    };

    let merge_result = merge_three_way(local, base, remote);
    match serde_json::to_string(&merge_result) {
        Ok(json) => match CString::new(json) {
            Ok(c_str) => c_str.into_raw(),
            Err(_) => std::ptr::null_mut(),
        },
        Err(_) => std::ptr::null_mut(),
    }
}

/// C-ABI wrapper for folder diff returning JSON string.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn maccompare_compare_folders_json(
    left_path_ptr: *const c_char,
    right_path_ptr: *const c_char,
    mode_int: i32,
    exclude_patterns_json_ptr: *const c_char,
) -> *mut c_char {
    if left_path_ptr.is_null() || right_path_ptr.is_null() {
        return std::ptr::null_mut();
    }

    let left_str = match unsafe { CStr::from_ptr(left_path_ptr) }.to_str() {
        Ok(s) => s,
        Err(_) => return std::ptr::null_mut(),
    };
    let right_str = match unsafe { CStr::from_ptr(right_path_ptr) }.to_str() {
        Ok(s) => s,
        Err(_) => return std::ptr::null_mut(),
    };

    let mode = match mode_int {
        1 => FolderCompareMode::DeepHash,
        2 => FolderCompareMode::FullByte,
        _ => FolderCompareMode::Quick,
    };

    let exclude_patterns: Vec<String> = if !exclude_patterns_json_ptr.is_null() {
        if let Ok(json_str) = unsafe { CStr::from_ptr(exclude_patterns_json_ptr) }.to_str() {
            serde_json::from_str::<Vec<String>>(json_str).unwrap_or_else(|_| vec![
                ".git/**".to_string(),
                ".DS_Store".to_string(),
                "node_modules/**".to_string(),
                "target/**".to_string(),
                "build/**".to_string(),
            ])
        } else {
            vec![
                ".git/**".to_string(),
                ".DS_Store".to_string(),
                "node_modules/**".to_string(),
                "target/**".to_string(),
                "build/**".to_string(),
            ]
        }
    } else {
        vec![
            ".git/**".to_string(),
            ".DS_Store".to_string(),
            "node_modules/**".to_string(),
            "target/**".to_string(),
            "build/**".to_string(),
        ]
    };

    let options = FolderDiffOptions {
        mode,
        exclude_patterns,
        include_patterns: vec![],
        recursive: true,
    };

    let left_path = Path::new(left_str);
    let right_path = Path::new(right_str);

    match compare_folders(left_path, right_path, &options) {
        Ok(entries) => match serde_json::to_string(&entries) {
            Ok(json) => match CString::new(json) {
                Ok(c_str) => c_str.into_raw(),
                Err(_) => std::ptr::null_mut(),
            },
            Err(_) => std::ptr::null_mut(),
        },
        Err(_) => std::ptr::null_mut(),
    }
}

// MARK: - Big-Data Excel Diff Session FFI

/// Create a new Excel Diff Session and compute differences.
/// Returns an opaque pointer to `ExcelDiffSession`. Caller must free with `maccompare_excel_free_session`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn maccompare_excel_create_session(
    left_path_ptr: *const c_char,
    right_path_ptr: *const c_char,
    options_json_ptr: *const c_char,
) -> *mut ExcelDiffSession {
    if left_path_ptr.is_null() || right_path_ptr.is_null() {
        return std::ptr::null_mut();
    }

    let left_path_str = match unsafe { CStr::from_ptr(left_path_ptr) }.to_str() {
        Ok(s) => s,
        Err(_) => return std::ptr::null_mut(),
    };
    let right_path_str = match unsafe { CStr::from_ptr(right_path_ptr) }.to_str() {
        Ok(s) => s,
        Err(_) => return std::ptr::null_mut(),
    };

    let rules: ExcelCompareRules = if !options_json_ptr.is_null() {
        if let Ok(json_str) = unsafe { CStr::from_ptr(options_json_ptr) }.to_str() {
            serde_json::from_str(json_str).unwrap_or_default()
        } else {
            ExcelCompareRules::default()
        }
    } else {
        ExcelCompareRules::default()
    };

    match ExcelDiffSession::new(Path::new(left_path_str), Path::new(right_path_str), &rules) {
        Ok(session) => Box::into_raw(Box::new(session)),
        Err(_) => std::ptr::null_mut(),
    }
}

/// Get workbook diff summary JSON string. Caller must free with `free_rust_string`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn maccompare_excel_get_summary(session_ptr: *mut ExcelDiffSession) -> *mut c_char {
    if session_ptr.is_null() {
        return std::ptr::null_mut();
    }

    let session = unsafe { &*session_ptr };
    let summary = session.get_summary();

    match serde_json::to_string(&summary) {
        Ok(json) => match CString::new(json) {
            Ok(c_str) => c_str.into_raw(),
            Err(_) => std::ptr::null_mut(),
        },
        Err(_) => std::ptr::null_mut(),
    }
}

/// Query viewport rows (start_row..start_row + count). Caller must free with `free_rust_string`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn maccompare_excel_get_viewport_json(
    session_ptr: *mut ExcelDiffSession,
    sheet_index: usize,
    start_row: usize,
    count: usize,
) -> *mut c_char {
    if session_ptr.is_null() {
        return std::ptr::null_mut();
    }

    let session = unsafe { &*session_ptr };
    let viewport = session.get_viewport(sheet_index, start_row, count);

    match serde_json::to_string(&viewport) {
        Ok(json) => match CString::new(json) {
            Ok(c_str) => c_str.into_raw(),
            Err(_) => std::ptr::null_mut(),
        },
        Err(_) => std::ptr::null_mut(),
    }
}

/// Find next/previous difference row index in O(log N). Returns -1 if not found.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn maccompare_excel_find_next_diff_row(
    session_ptr: *mut ExcelDiffSession,
    sheet_index: usize,
    current_row: usize,
    backwards: bool,
) -> i64 {
    if session_ptr.is_null() {
        return -1;
    }

    let session = unsafe { &*session_ptr };
    session
        .find_next_diff_row(sheet_index, current_row, backwards)
        .map(|idx| idx as i64)
        .unwrap_or(-1)
}

/// Destroy an Excel diff session and reclaim memory.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn maccompare_excel_free_session(session_ptr: *mut ExcelDiffSession) {
    if !session_ptr.is_null() {
        unsafe {
            drop(Box::from_raw(session_ptr));
        }
    }
}

/// Free a CString allocated by Rust.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn free_rust_string(ptr: *mut c_char) {
    if !ptr.is_null() {
        unsafe {
            drop(CString::from_raw(ptr));
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::ffi::CString;

    #[test]
    fn test_ffi_compare_text() {
        let left = CString::new("Hello World").unwrap();
        let right = CString::new("Hello Rust").unwrap();

        unsafe {
            let json_ptr = maccompare_compare_text_json(left.as_ptr(), right.as_ptr(), false, false);
            assert!(!json_ptr.is_null());
            let json = CStr::from_ptr(json_ptr).to_str().unwrap();
            assert!(json.contains("Modified"));
            free_rust_string(json_ptr);
        }
    }

    #[test]
    fn test_ffi_availability_and_version() {
        assert!(maccompare_is_available());
        unsafe {
            let ver_ptr = maccompare_version();
            assert!(!ver_ptr.is_null());
            let ver = CStr::from_ptr(ver_ptr).to_str().unwrap();
            assert!(ver.contains("MacCompare Core"));
            free_rust_string(ver_ptr);
        }
    }
}
