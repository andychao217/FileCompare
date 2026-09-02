#ifndef MACCOMPARE_CORE_H
#define MACCOMPARE_CORE_H

#include <stdbool.h>
#include <stdint.h>
#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

/// Returns true if the Rust core engine is linked and functional.
bool maccompare_is_available(void);

/// Returns the Rust core version string. Caller must free using `free_rust_string`.
char *maccompare_version(void);

/// 2-way Myers text comparison returning a JSON string.
/// Caller must free using `free_rust_string`.
char *maccompare_compare_text_json(
    const char *left_ptr,
    const char *right_ptr,
    bool ignore_whitespace,
    bool ignore_case
);

/// 3-way merge returning a JSON string.
/// Caller must free using `free_rust_string`.
char *maccompare_merge_three_way_json(
    const char *local_ptr,
    const char *base_ptr,
    const char *remote_ptr
);

/// Directory comparison returning a JSON string.
/// mode_int: 0=Quick, 1=DeepHash(CRC32), 2=FullByte
/// exclude_patterns_json_ptr: optional JSON array string, e.g. "[\".git/**\", \".DS_Store\"]"
/// Caller must free using `free_rust_string`.
char *maccompare_compare_folders_json(
    const char *left_path_ptr,
    const char *right_path_ptr,
    int32_t mode_int,
    const char *exclude_patterns_json_ptr
);

// MARK: - Big-Data Spreadsheet Diff Session

/// Create a new Excel / CSV Diff Session. Returns an opaque pointer handle.
/// Caller must free with `maccompare_excel_free_session`.
void *maccompare_excel_create_session(
    const char *left_path_ptr,
    const char *right_path_ptr,
    const char *options_json_ptr
);

/// Get workbook diff summary JSON string. Caller must free with `free_rust_string`.
char *maccompare_excel_get_summary(void *session_ptr);

/// Query viewport rows (start_row..start_row + count). Caller must free with `free_rust_string`.
char *maccompare_excel_get_viewport_json(
    void *session_ptr,
    size_t sheet_index,
    size_t start_row,
    size_t count
);

/// Find next/previous difference row index in O(log N). Returns -1 if not found.
int64_t maccompare_excel_find_next_diff_row(
    void *session_ptr,
    size_t sheet_index,
    size_t current_row,
    bool backwards
);

/// Destroy an Excel diff session and reclaim memory.
void maccompare_excel_free_session(void *session_ptr);

/// Free a C-string allocated by Rust.
void free_rust_string(char *ptr);

#ifdef __cplusplus
}
#endif

#endif /* MACCOMPARE_CORE_H */
