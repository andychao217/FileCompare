//! MacCompare FFI layer for Swift integration.

use diff_core::{compare_text, merge_three_way, DiffOptions};
use fs_scanner::{compare_folders, FolderDiffOptions};
use std::ffi::{CStr, CString};
use std::os::raw::c_char;
use std::path::Path;

/// C-ABI wrapper for 2-way text diff returning JSON string.
/// Caller must free returned string using `free_rust_string`.
#[no_mangle]
pub unsafe extern "C" fn maccompare_compare_text_json(
    left_ptr: *const c_char,
    right_ptr: *const c_char,
    ignore_whitespace: bool,
    ignore_case: bool,
) -> *mut c_char {
    if left_ptr.is_null() || right_ptr.is_null() {
        return std::ptr::null_mut();
    }

    let left = match CStr::from_ptr(left_ptr).to_str() {
        Ok(s) => s,
        Err(_) => return std::ptr::null_mut(),
    };
    let right = match CStr::from_ptr(right_ptr).to_str() {
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
#[no_mangle]
pub unsafe extern "C" fn maccompare_merge_three_way_json(
    local_ptr: *const c_char,
    base_ptr: *const c_char,
    remote_ptr: *const c_char,
) -> *mut c_char {
    if local_ptr.is_null() || base_ptr.is_null() || remote_ptr.is_null() {
        return std::ptr::null_mut();
    }

    let local = match CStr::from_ptr(local_ptr).to_str() {
        Ok(s) => s,
        Err(_) => return std::ptr::null_mut(),
    };
    let base = match CStr::from_ptr(base_ptr).to_str() {
        Ok(s) => s,
        Err(_) => return std::ptr::null_mut(),
    };
    let remote = match CStr::from_ptr(remote_ptr).to_str() {
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
#[no_mangle]
pub unsafe extern "C" fn maccompare_compare_folders_json(
    left_path_ptr: *const c_char,
    right_path_ptr: *const c_char,
    mode_int: i32,
) -> *mut c_char {
    if left_path_ptr.is_null() || right_path_ptr.is_null() {
        return std::ptr::null_mut();
    }

    let left_str = match CStr::from_ptr(left_path_ptr).to_str() {
        Ok(s) => s,
        Err(_) => return std::ptr::null_mut(),
    };
    let right_str = match CStr::from_ptr(right_path_ptr).to_str() {
        Ok(s) => s,
        Err(_) => return std::ptr::null_mut(),
    };

    let mode = match mode_int {
        1 => fs_scanner::FolderCompareMode::DeepHash,
        2 => fs_scanner::FolderCompareMode::FullByte,
        _ => fs_scanner::FolderCompareMode::Quick,
    };

    let options = FolderDiffOptions {
        mode,
        exclude_patterns: vec![
            ".git/**".to_string(),
            ".DS_Store".to_string(),
            "node_modules/**".to_string(),
            "target/**".to_string(),
            "build/**".to_string(),
        ],
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

/// Free a CString allocated by Rust.
#[no_mangle]
pub unsafe extern "C" fn free_rust_string(ptr: *mut c_char) {
    if !ptr.is_null() {
        drop(CString::from_raw(ptr));
    }
}
