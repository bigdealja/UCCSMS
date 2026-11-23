using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Persistence;

using SMS.Application.GradeImport;

namespace SMS.Infrastructure.GradeImport;

/// <summary>Validates a parsed sheet against the DB and, on commit, writes grades as one
/// undoable batch. Replaces the legacy ExportFinalGrades/ExportCwfgGrades flow — but with
/// a preview + validation step the old tool never had.</summary>
public class GradeImportService
{
    private readonly SmsDbContext _db;
    public GradeImportService(SmsDbContext db) => _db = db;

    public async Task<ImportPreview> PreviewAsync(
        int termId, int courseId, ParsedSheet sheet, GradeImportMapping map, CancellationToken ct = default)
    {
        int Col(string? name) => name is null ? -1 : IndexOfHeader(sheet.Headers, name);
        int idCol = Col(map.StudentNumberColumn);
        int letterCol = Col(map.LetterGradeColumn);
        int cwCol = Col(map.CourseworkColumn);
        int feCol = Col(map.FinalExamColumn);

        if (idCol < 0)
            return new ImportPreview(termId, courseId, Array.Empty<PreviewRow>(), 0, 0, 0);

        // Pre-load the students referenced and the existing grades for this course/term.
        var numbers = sheet.Rows.Select(r => Cell(r, idCol)).Where(s => s.Length > 0).Distinct().ToList();
        var students = await _db.Students.AsNoTracking()
            .Where(s => numbers.Contains(s.StudentNumber))
            .ToDictionaryAsync(s => s.StudentNumber, ct);
        var existing = await _db.Enrollments.AsNoTracking()
            .Where(e => e.TermId == termId && e.CourseId == courseId)
            .Select(e => new { e.StudentId, e.LetterGrade, e.CourseworkMark, e.FinalExamMark })
            .ToListAsync(ct);
        var graded = existing.Where(e => e.LetterGrade != null || e.CourseworkMark != null || e.FinalExamMark != null)
                             .Select(e => e.StudentId).ToHashSet();

        var rows = new List<PreviewRow>();
        int rn = 1;
        foreach (var r in sheet.Rows)
        {
            rn++;
            var num = Cell(r, idCol);
            var messages = new List<string>();
            if (num.Length == 0) continue; // skip blank lines

            Student? stu = students.GetValueOrDefault(num);
            string? letter = letterCol >= 0 ? NullIfEmpty(Cell(r, letterCol)) : null;
            double? cw = ParseNum(cwCol >= 0 ? Cell(r, cwCol) : "", messages, "coursework");
            double? fe = ParseNum(feCol >= 0 ? Cell(r, feCol) : "", messages, "final exam");

            var status = RowStatus.Ok;
            if (stu is null) { messages.Add($"No student with number '{num}'."); status = RowStatus.Error; }
            if (letter is null && cw is null && fe is null)
            { messages.Add("No grade value to import."); status = RowStatus.Error; }

            bool isNew = stu is not null && !graded.Contains(stu.Id);
            if (!isNew && stu is not null && status != RowStatus.Error)
            { messages.Add("Would overwrite an existing grade."); if (status == RowStatus.Ok) status = RowStatus.Warning; }

            rows.Add(new PreviewRow(rn, num, stu is null ? null : $"{stu.LastName}, {stu.FirstName}",
                letter, cw, fe, isNew, status, messages));
        }

        return new ImportPreview(termId, courseId, rows,
            rows.Count(x => x.Status == RowStatus.Ok),
            rows.Count(x => x.Status == RowStatus.Warning),
            rows.Count(x => x.Status == RowStatus.Error));
    }

    /// <summary>Applies every non-error row as one batch. Errors are skipped.
    /// All writes share one ImportBatchId so the whole run can be undone.</summary>
    public async Task<CommitResult> CommitAsync(
        int termId, int courseId, ImportPreview preview, string user, CancellationToken ct = default)
    {
        var batchId = Guid.NewGuid();
        int applied = 0, skipped = 0;

        var numbers = preview.Rows.Where(r => r.Status != RowStatus.Error).Select(r => r.StudentNumber).ToList();
        var students = await _db.Students.Where(s => numbers.Contains(s.StudentNumber))
            .ToDictionaryAsync(s => s.StudentNumber, ct);
        var enrollments = await _db.Enrollments
            .Where(e => e.TermId == termId && e.CourseId == courseId).ToListAsync(ct);
        var byStudent = enrollments.ToDictionary(e => e.StudentId);

        foreach (var row in preview.Rows)
        {
            if (row.Status == RowStatus.Error) { skipped++; continue; }
            if (!students.TryGetValue(row.StudentNumber, out var stu)) { skipped++; continue; }

            if (!byStudent.TryGetValue(stu.Id, out var e))
            {
                e = new Enrollment { StudentId = stu.Id, CourseId = courseId, TermId = termId,
                                     Status = EnrollmentStatus.Registered };
                _db.Enrollments.Add(e);
                byStudent[stu.Id] = e;
            }
            e.LetterGrade = row.LetterGrade;
            e.CourseworkMark = row.Coursework;
            e.FinalExamMark = row.FinalExam;
            e.GradeSource = "Import";
            e.GradedUtc = DateTimeOffset.UtcNow;
            e.ImportBatchId = batchId;
            applied++;
        }

        await _db.SaveChangesAsync(ct);
        return new CommitResult(batchId, applied, skipped);
    }

    static int IndexOfHeader(IReadOnlyList<string> headers, string name)
    {
        for (int i = 0; i < headers.Count; i++)
            if (string.Equals(headers[i].Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }
    static string Cell(IReadOnlyList<string> row, int i) => i >= 0 && i < row.Count ? (row[i] ?? "").Trim() : "";
    static string? NullIfEmpty(string s) => s.Length == 0 ? null : s;
    static double? ParseNum(string s, List<string> messages, string field)
    {
        s = s.Trim();
        if (s.Length == 0) return null;
        if (double.TryParse(s, out var v)) return v;
        messages.Add($"'{s}' is not a valid {field} mark.");
        return null;
    }
}
