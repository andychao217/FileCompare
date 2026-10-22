using System.IO;
using WinCompare.Core.Models;

namespace WinCompare.Core.Services;

public enum ValidationPromptType
{
    NoticeOnly,
    SuggestSwitch
}

public sealed class ValidationResult
{
    public bool IsValid { get; set; }
    public string TitleKey { get; set; } = "dialogNoticeTitle";
    public string MessageKey { get; set; } = "dialogNoticeTitle";
    public ValidationPromptType PromptType { get; set; } = ValidationPromptType.NoticeOnly;
    public CompareType? RecommendedMode { get; set; }
    public string TargetPath { get; set; } = string.Empty;

    public static ValidationResult Valid(string path) => new()
    {
        IsValid = true,
        TargetPath = path
    };
}

public static class FileFormatValidator
{
    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".ico", ".tiff", ".psd", ".ai",
        ".zip", ".tar", ".gz", ".tgz", ".bz2", ".7z", ".rar", ".xz", ".dmg", ".pkg", ".iso",
        ".exe", ".dll", ".dylib", ".so", ".a", ".bin", ".o", ".obj", ".class", ".jar",
        ".mp3", ".mp4", ".mov", ".wav", ".m4a", ".flac", ".avi", ".mkv",
        ".ttf", ".otf", ".woff", ".woff2", ".eot", ".db", ".sqlite", ".sqlite3"
    };

    private static readonly HashSet<string> WordExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".docx", ".doc", ".rtf"
    };

    private static readonly HashSet<string> PdfExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf"
    };

    private static readonly HashSet<string> ExcelExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx", ".xls", ".xlsm", ".csv", ".tsv"
    };

    public static ValidationResult Validate(string path, CompareType expectedMode)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new ValidationResult
            {
                IsValid = false,
                TitleKey = "dialogNoticeTitle",
                MessageKey = "errFolderDroppedInFileMode",
                PromptType = ValidationPromptType.NoticeOnly,
                TargetPath = path ?? string.Empty
            };
        }

        bool isDir = Directory.Exists(path);
        bool fileExists = File.Exists(path);

        if (!isDir && !fileExists)
        {
            return new ValidationResult
            {
                IsValid = false,
                TitleKey = "dialogNoticeTitle",
                MessageKey = "errFolderDroppedInFileMode",
                PromptType = ValidationPromptType.NoticeOnly,
                TargetPath = path
            };
        }

        // 1. Directory Checks
        if (isDir)
        {
            if (expectedMode == CompareType.FolderDiff)
            {
                return ValidationResult.Valid(path);
            }
            else
            {
                return new ValidationResult
                {
                    IsValid = false,
                    TitleKey = "suggestSwitchModeTitle",
                    MessageKey = "errFolderDroppedInFileMode",
                    PromptType = ValidationPromptType.SuggestSwitch,
                    RecommendedMode = CompareType.FolderDiff,
                    TargetPath = path
                };
            }
        }

        // 2. Single File in FolderDiff Mode
        if (expectedMode == CompareType.FolderDiff)
        {
            return new ValidationResult
            {
                IsValid = false,
                TitleKey = "dialogNoticeTitle",
                MessageKey = "errFileDroppedInFolderMode",
                PromptType = ValidationPromptType.NoticeOnly,
                TargetPath = path
            };
        }

        string ext = Path.GetExtension(path);

        // 3. Mode Specific Validations
        switch (expectedMode)
        {
            case CompareType.TextDiff:
            case CompareType.ThreeWayMerge:
                if (PdfExtensions.Contains(ext))
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        TitleKey = "suggestSwitchModeTitle",
                        MessageKey = "suggestSwitchToPdf",
                        PromptType = ValidationPromptType.SuggestSwitch,
                        RecommendedMode = CompareType.PdfDiff,
                        TargetPath = path
                    };
                }
                if (WordExtensions.Contains(ext))
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        TitleKey = "suggestSwitchModeTitle",
                        MessageKey = "suggestSwitchToWord",
                        PromptType = ValidationPromptType.SuggestSwitch,
                        RecommendedMode = CompareType.WordDiff,
                        TargetPath = path
                    };
                }
                if (ExcelExtensions.Contains(ext) && !string.Equals(ext, ".csv", StringComparison.OrdinalIgnoreCase) && !string.Equals(ext, ".tsv", StringComparison.OrdinalIgnoreCase))
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        TitleKey = "suggestSwitchModeTitle",
                        MessageKey = "suggestSwitchToExcel",
                        PromptType = ValidationPromptType.SuggestSwitch,
                        RecommendedMode = CompareType.ExcelDiff,
                        TargetPath = path
                    };
                }
                if (BinaryExtensions.Contains(ext))
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        TitleKey = "dialogNoticeTitle",
                        MessageKey = "errBinaryFileNotSupported",
                        PromptType = ValidationPromptType.NoticeOnly,
                        TargetPath = path
                    };
                }
                return ValidationResult.Valid(path);

            case CompareType.WordDiff:
                if (WordExtensions.Contains(ext))
                {
                    return ValidationResult.Valid(path);
                }
                if (PdfExtensions.Contains(ext))
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        TitleKey = "suggestSwitchModeTitle",
                        MessageKey = "suggestSwitchToPdf",
                        PromptType = ValidationPromptType.SuggestSwitch,
                        RecommendedMode = CompareType.PdfDiff,
                        TargetPath = path
                    };
                }
                if (ExcelExtensions.Contains(ext))
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        TitleKey = "suggestSwitchModeTitle",
                        MessageKey = "suggestSwitchToExcel",
                        PromptType = ValidationPromptType.SuggestSwitch,
                        RecommendedMode = CompareType.ExcelDiff,
                        TargetPath = path
                    };
                }
                return new ValidationResult
                {
                    IsValid = false,
                    TitleKey = "dialogNoticeTitle",
                    MessageKey = "errWordInvalidFormat",
                    PromptType = ValidationPromptType.NoticeOnly,
                    TargetPath = path
                };

            case CompareType.PdfDiff:
                if (PdfExtensions.Contains(ext))
                {
                    return ValidationResult.Valid(path);
                }
                if (WordExtensions.Contains(ext))
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        TitleKey = "suggestSwitchModeTitle",
                        MessageKey = "suggestSwitchToWord",
                        PromptType = ValidationPromptType.SuggestSwitch,
                        RecommendedMode = CompareType.WordDiff,
                        TargetPath = path
                    };
                }
                if (ExcelExtensions.Contains(ext))
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        TitleKey = "suggestSwitchModeTitle",
                        MessageKey = "suggestSwitchToExcel",
                        PromptType = ValidationPromptType.SuggestSwitch,
                        RecommendedMode = CompareType.ExcelDiff,
                        TargetPath = path
                    };
                }
                return new ValidationResult
                {
                    IsValid = false,
                    TitleKey = "dialogNoticeTitle",
                    MessageKey = "errPdfInvalidFormat",
                    PromptType = ValidationPromptType.NoticeOnly,
                    TargetPath = path
                };

            case CompareType.ExcelDiff:
                if (ExcelExtensions.Contains(ext))
                {
                    return ValidationResult.Valid(path);
                }
                if (PdfExtensions.Contains(ext))
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        TitleKey = "suggestSwitchModeTitle",
                        MessageKey = "suggestSwitchToPdf",
                        PromptType = ValidationPromptType.SuggestSwitch,
                        RecommendedMode = CompareType.PdfDiff,
                        TargetPath = path
                    };
                }
                if (WordExtensions.Contains(ext))
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        TitleKey = "suggestSwitchModeTitle",
                        MessageKey = "suggestSwitchToWord",
                        PromptType = ValidationPromptType.SuggestSwitch,
                        RecommendedMode = CompareType.WordDiff,
                        TargetPath = path
                    };
                }
                return new ValidationResult
                {
                    IsValid = false,
                    TitleKey = "dialogNoticeTitle",
                    MessageKey = "errExcelInvalidFormat",
                    PromptType = ValidationPromptType.NoticeOnly,
                    TargetPath = path
                };

            default:
                return ValidationResult.Valid(path);
        }
    }
}
