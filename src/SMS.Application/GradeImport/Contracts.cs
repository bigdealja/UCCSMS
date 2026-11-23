namespace SMS.Application.GradeImport;

/// <summary>A sheet parsed from an uploaded file: header names + rows of cell strings.
/// The parser (Excel/CSV) lives in Infrastructure; Application stays dependency-free.</summary>
public record ParsedSheet(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows);

public interface IGradeFileParser
{
    /// <summary>Parse .xlsx or .csv into a header row + data rows. No named ranges required
    /// (the legacy tool needed hand-typed Excel named ranges via 32-bit Jet).</summary>
    ParsedSheet Parse(Stream file, string fileName);
}

/// <summary>Which column (by header name) supplies each field. Chosen in the UI — the
/// replacement for the legacy "type the named range" step.</summary>
public record GradeImportMapping(
    string StudentNumberColumn,
    string? LetterGradeColumn,
    string? CourseworkColumn,
    string? FinalExamColumn);

public enum RowStatus { Ok, Warning, Error }

public record PreviewRow(
    int RowNumber,
    string StudentNumber,
    string? ResolvedStudentName,
    string? LetterGrade,
    double? Coursework,
    double? FinalExam,
    bool IsNewGrade,          // false = would overwrite an existing grade
    RowStatus Status,
    IReadOnlyList<string> Messages);

public record ImportPreview(
    int TermId, int CourseId,
    IReadOnlyList<PreviewRow> Rows,
    int OkCount, int WarningCount, int ErrorCount);

public record CommitResult(Guid BatchId, int Applied, int Skipped);
