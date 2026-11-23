using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SMS.Domain.Enums;
using SMS.Infrastructure.Grading;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Pdf;

/// <summary>Generates official PDF documents from data already in the system (transcripts,
/// acceptance letters). Uses QuestPDF under its Community licence — see the README; institutions
/// above the revenue threshold must hold a QuestPDF Professional licence.</summary>
public class PdfService
{
    private readonly SmsDbContext _db;
    private readonly GradingService _grading;
    static PdfService() { QuestPDF.Settings.License = LicenseType.Community; }
    public PdfService(SmsDbContext db, GradingService grading) { _db = db; _grading = grading; }

    public async Task<byte[]> TranscriptAsync(int studentId, CancellationToken ct = default)
    {
        var s = await _db.Students.AsNoTracking().FirstOrDefaultAsync(x => x.Id == studentId, ct)
                ?? throw new InvalidOperationException("Student not found.");
        var programme = await _db.Programmes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == s.ProgrammeId, ct);
        var rows = await _db.Enrollments.AsNoTracking().Where(e => e.StudentId == studentId)
            .Select(e => new { e.Course.Code, e.Course.Title, Term = e.Term.Name + " " + e.Term.Year, e.Course.Credits, e.LetterGrade })
            .ToListAsync(ct);
        var gpa = await _grading.ComputeGpaAsync(studentId, ct);

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Margin(40);
                page.Header().Column(h =>
                {
                    h.Item().Text("University of the Commonwealth Caribbean").Bold().FontSize(16);
                    h.Item().Text("Official Academic Transcript").FontSize(12).FontColor(Colors.Grey.Darken1);
                    h.Item().PaddingTop(4).LineHorizontal(1);
                });
                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Item().Text($"Student: {s.FirstName} {s.LastName}");
                    col.Item().Text($"Student Number: {s.StudentNumber}");
                    col.Item().Text($"Programme: {programme?.Name ?? "-"}");
                    col.Item().Text($"Status: {s.Status}");
                    col.Item().PaddingTop(12).Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.ConstantColumn(70); c.RelativeColumn(); c.ConstantColumn(90); c.ConstantColumn(55); c.ConstantColumn(55); });
                        void H(string x) => t.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(x).Bold();
                        H("Code"); H("Course"); H("Term"); H("Credits"); H("Grade");
                        foreach (var r in rows)
                        {
                            t.Cell().Padding(4).Text(r.Code);
                            t.Cell().Padding(4).Text(r.Title);
                            t.Cell().Padding(4).Text(r.Term);
                            t.Cell().Padding(4).Text(r.Credits.ToString());
                            t.Cell().Padding(4).Text(r.LetterGrade ?? "-");
                        }
                    });
                    col.Item().PaddingTop(12).Text($"Cumulative GPA: {gpa:0.00}").Bold();
                });
                page.Footer().AlignCenter().Text($"Generated {DateTime.UtcNow:yyyy-MM-dd} · This is a system-generated transcript.")
                    .FontSize(8).FontColor(Colors.Grey.Medium);
            });
        }).GeneratePdf();
    }

    public async Task<byte[]> AcceptanceLetterAsync(int applicationId, CancellationToken ct = default)
    {
        var a = await _db.Applications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == applicationId, ct)
                ?? throw new InvalidOperationException("Application not found.");
        var programme = await _db.Programmes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == a.ProgrammeId, ct);
        var term = await _db.Terms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == a.IntakeTermId, ct);
        string category = a.Decision == AdmissionCategory.None ? "admission" : Humanize(a.Decision.ToString());

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Margin(50);
                page.Header().Column(h =>
                {
                    h.Item().Text("University of the Commonwealth Caribbean").Bold().FontSize(16);
                    h.Item().Text("Office of Admissions").FontSize(10).FontColor(Colors.Grey.Darken1);
                });
                page.Content().PaddingVertical(16).Column(col =>
                {
                    col.Spacing(10);
                    col.Item().AlignRight().Text(DateTime.UtcNow.ToString("MMMM d, yyyy"));
                    col.Item().Text($"Dear {a.FirstName} {a.LastName},");
                    col.Item().Text($"Congratulations! On behalf of the University of the Commonwealth Caribbean, " +
                        $"it is my pleasure to offer you {category} to the {programme?.Name ?? "programme"} " +
                        $"commencing {term?.Name} {term?.Year}.");
                    col.Item().Text("Please complete and return the Placement Offer Response Form to confirm your acceptance, " +
                        "and submit any outstanding supporting documents within ten (10) business days of this letter.");
                    col.Item().Text($"Application reference: {a.ApplicationNumber}");
                    col.Item().PaddingTop(20).Text("Sincerely,");
                    col.Item().Text("Registrar");
                    col.Item().Text("University of the Commonwealth Caribbean");
                });
                page.Footer().AlignCenter().Text($"Generated {DateTime.UtcNow:yyyy-MM-dd} · Ref {a.ApplicationNumber}")
                    .FontSize(8).FontColor(Colors.Grey.Medium);
            });
        }).GeneratePdf();
    }

    static string Humanize(string pascal) =>
        string.Concat(pascal.Select((ch, i) => i > 0 && char.IsUpper(ch) ? " " + char.ToLower(ch) : char.ToString(char.ToLower(ch)))).Trim();
}
