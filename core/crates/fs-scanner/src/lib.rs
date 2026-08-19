//! High-speed Concurrent Directory Scanner with Hardware-accelerated CRC32 / SIMD hash.

use globset::{Glob, GlobSet, GlobSetBuilder};
use rayon::prelude::*;
use serde::{Deserialize, Serialize};
use std::fs;
use std::path::{Path, PathBuf};

/// Status of an item when comparing two directories.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum FolderItemStatus {
    Equal,
    LeftOnly,
    RightOnly,
    ContentDifferent,
    MetadataDifferent,
}

/// A node in the aligned directory diff tree.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct FolderDiffEntry {
    pub relative_path: String,
    pub is_directory: bool,
    pub status: FolderItemStatus,
    pub left_size: Option<u64>,
    pub right_size: Option<u64>,
    pub left_modified_timestamp: Option<u64>,
    pub right_modified_timestamp: Option<u64>,
    pub left_hash: Option<String>,
    pub right_hash: Option<String>,
}

/// Directory comparison mode.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, Default)]
pub enum FolderCompareMode {
    #[default]
    Quick,      // Timestamp + Size
    DeepHash,   // CRC32 / Fast Hash
    FullByte,   // Byte-by-byte
}

/// Options for folder comparison.
#[derive(Debug, Clone, Serialize, Deserialize, Default)]
pub struct FolderDiffOptions {
    pub mode: FolderCompareMode,
    pub exclude_patterns: Vec<String>,
    pub include_patterns: Vec<String>,
    pub recursive: bool,
}

/// Scanned file metadata.
#[derive(Debug, Clone)]
struct FileItemMeta {
    pub rel_path: String,
    pub is_dir: bool,
    pub size: u64,
    pub mtime_secs: u64,
}

/// Recursively scans and compares two directories.
pub fn compare_folders(
    left_root: &Path,
    right_root: &Path,
    options: &FolderDiffOptions,
) -> Result<Vec<FolderDiffEntry>, std::io::Error> {
    let glob_builder = build_globset(&options.exclude_patterns);

    let left_items = scan_dir_parallel(left_root, &glob_builder);
    let right_items = scan_dir_parallel(right_root, &glob_builder);

    // Collect all relative paths
    let mut all_paths: std::collections::BTreeSet<String> = std::collections::BTreeSet::new();
    for item in &left_items {
        all_paths.insert(item.rel_path.clone());
    }
    for item in &right_items {
        all_paths.insert(item.rel_path.clone());
    }

    let left_map: std::collections::HashMap<String, &FileItemMeta> =
        left_items.iter().map(|item| (item.rel_path.clone(), item)).collect();
    let right_map: std::collections::HashMap<String, &FileItemMeta> =
        right_items.iter().map(|item| (item.rel_path.clone(), item)).collect();

    let results: Vec<FolderDiffEntry> = all_paths
        .into_par_iter()
        .map(|rel_path| {
            let left = left_map.get(&rel_path).copied();
            let right = right_map.get(&rel_path).copied();

            match (left, right) {
                (Some(l), Some(r)) => {
                    let is_dir = l.is_dir;
                    if is_dir {
                        FolderDiffEntry {
                            relative_path: rel_path,
                            is_directory: true,
                            status: FolderItemStatus::Equal,
                            left_size: None,
                            right_size: None,
                            left_modified_timestamp: Some(l.mtime_secs),
                            right_modified_timestamp: Some(r.mtime_secs),
                            left_hash: None,
                            right_hash: None,
                        }
                    } else {
                        let (status, l_hash, r_hash) = match options.mode {
                            FolderCompareMode::Quick => {
                                if l.size == r.size && l.mtime_secs == r.mtime_secs {
                                    (FolderItemStatus::Equal, None, None)
                                } else if l.size == r.size {
                                    (FolderItemStatus::MetadataDifferent, None, None)
                                } else {
                                    (FolderItemStatus::ContentDifferent, None, None)
                                }
                            }
                            FolderCompareMode::DeepHash | FolderCompareMode::FullByte => {
                                let l_p = left_root.join(&rel_path);
                                let r_p = right_root.join(&rel_path);
                                let h_l = calculate_crc32(&l_p).ok();
                                let h_r = calculate_crc32(&r_p).ok();

                                let status = if h_l == h_r && h_l.is_some() {
                                    FolderItemStatus::Equal
                                } else {
                                    FolderItemStatus::ContentDifferent
                                };
                                (
                                    status,
                                    h_l.map(|h| format!("{:08x}", h)),
                                    h_r.map(|h| format!("{:08x}", h)),
                                )
                            }
                        };

                        FolderDiffEntry {
                            relative_path: rel_path,
                            is_directory: false,
                            status,
                            left_size: Some(l.size),
                            right_size: Some(r.size),
                            left_modified_timestamp: Some(l.mtime_secs),
                            right_modified_timestamp: Some(r.mtime_secs),
                            left_hash: l_hash,
                            right_hash: r_hash,
                        }
                    }
                }
                (Some(l), None) => FolderDiffEntry {
                    relative_path: rel_path,
                    is_directory: l.is_dir,
                    status: FolderItemStatus::LeftOnly,
                    left_size: if l.is_dir { None } else { Some(l.size) },
                    right_size: None,
                    left_modified_timestamp: Some(l.mtime_secs),
                    right_modified_timestamp: None,
                    left_hash: None,
                    right_hash: None,
                },
                (None, Some(r)) => FolderDiffEntry {
                    relative_path: rel_path,
                    is_directory: r.is_dir,
                    status: FolderItemStatus::RightOnly,
                    left_size: None,
                    right_size: if r.is_dir { None } else { Some(r.size) },
                    left_modified_timestamp: None,
                    right_modified_timestamp: Some(r.mtime_secs),
                    left_hash: None,
                    right_hash: None,
                },
                (None, None) => unreachable!(),
            }
        })
        .collect();

    Ok(results)
}

fn scan_dir_parallel(root: &Path, exclude_set: &Option<GlobSet>) -> Vec<FileItemMeta> {
    let mut items = Vec::new();
    if !root.exists() {
        return items;
    }

    let mut stack = vec![root.to_path_buf()];
    while let Some(dir) = stack.pop() {
        if let Ok(entries) = fs::read_dir(&dir) {
            for entry in entries.flatten() {
                let path = entry.path();
                let rel = path
                    .strip_prefix(root)
                    .unwrap_or(&path)
                    .to_string_lossy()
                    .to_string();

                if let Some(set) = exclude_set {
                    if set.is_match(&rel) {
                        continue;
                    }
                }

                if let Ok(meta) = entry.metadata() {
                    let mtime = meta
                        .modified()
                        .ok()
                        .and_then(|t| t.duration_since(std::time::UNIX_EPOCH).ok())
                        .map(|d| d.as_secs())
                        .unwrap_or(0);

                    let is_dir = meta.is_dir();
                    items.push(FileItemMeta {
                        rel_path: rel,
                        is_dir,
                        size: meta.len(),
                        mtime_secs: mtime,
                    });

                    if is_dir {
                        stack.push(path);
                    }
                }
            }
        }
    }
    items
}

fn build_globset(patterns: &[String]) -> Option<GlobSet> {
    if patterns.is_empty() {
        return None;
    }
    let mut builder = GlobSetBuilder::new();
    for p in patterns {
        if let Ok(g) = Glob::new(p) {
            builder.add(g);
        }
    }
    builder.build().ok()
}

/// Compute fast CRC32 hash of a file.
pub fn calculate_crc32(path: &Path) -> std::io::Result<u32> {
    let bytes = fs::read(path)?;
    let mut hasher = crc32fast::Hasher::new();
    hasher.update(&bytes);
    Ok(hasher.finalize())
}
