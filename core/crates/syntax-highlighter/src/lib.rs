//! Syntax highlighter and AST tokenizer abstractions for MacCompare.

use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum HighlightCategory {
    Keyword,
    StringLiteral,
    NumberLiteral,
    Comment,
    TypeIdentifier,
    Function,
    Variable,
    Operator,
    Punctuation,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct HighlightSpan {
    pub start_byte: usize,
    pub end_byte: usize,
    pub category: HighlightCategory,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum SupportedLanguage {
    Rust,
    Swift,
    Python,
    JavaScript,
    TypeScript,
    Go,
    Java,
    Cpp,
    Json,
    Yaml,
    Markdown,
    PlainText,
}

impl SupportedLanguage {
    pub fn from_extension(ext: &str) -> Self {
        match ext.to_lowercase().as_str() {
            "rs" => Self::Rust,
            "swift" => Self::Swift,
            "py" => Self::Python,
            "js" | "mjs" => Self::JavaScript,
            "ts" | "tsx" => Self::TypeScript,
            "go" => Self::Go,
            "java" => Self::Java,
            "cpp" | "c" | "h" | "hpp" => Self::Cpp,
            "json" => Self::Json,
            "yaml" | "yml" => Self::Yaml,
            "md" | "markdown" => Self::Markdown,
            _ => Self::PlainText,
        }
    }
}

/// Tokenize and highlight input code for a given language.
pub fn highlight_tokens(_content: &str, _language: SupportedLanguage) -> Vec<HighlightSpan> {
    // Tree-sitter incremental highlight integration hook
    Vec::new()
}
