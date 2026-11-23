using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.QuestionPapers;

/// <summary>Question-paper lifecycle: register expected papers, submit versions, move through the
/// secure workflow (with an external-review gate before approval), record print runs and dispatch,
/// and handle return + witnessed destruction or emergency withdrawal.</summary>
public class QuestionPaperService
{
    private readonly SmsDbContext _db;
    public QuestionPaperService(SmsDbContext db) => _db = db;

    public async Task<QuestionPaper> RegisterAsync(int courseId, int termId, string title, DateOnly deadline,
        string? author, bool externalReviewRequired, CancellationToken ct = default)
    {
        var p = new QuestionPaper { CourseId = courseId, TermId = termId, Title = title, SubmissionDeadline = deadline,
            AuthorLecturer = author, ExternalReviewRequired = externalReviewRequired };
        _db.QuestionPapers.Add(p); await _db.SaveChangesAsync(ct);
        return p;
    }

    public async Task<QuestionPaperVersion> SubmitVersionAsync(int paperId, string fileName, int? documentId, string by, string? notes, CancellationToken ct = default)
    {
        var p = await _db.QuestionPapers.FirstOrDefaultAsync(x => x.Id == paperId, ct)
                ?? throw new InvalidOperationException("Paper not found.");
        if (p.Status >= PaperStatus.Released)
            throw new InvalidOperationException("Cannot submit a new version after release.");
        var v = new QuestionPaperVersion { QuestionPaperId = paperId, VersionNumber = p.CurrentVersion + 1,
            FileName = fileName, DocumentId = documentId, SubmittedBy = by, Notes = notes };
        _db.QuestionPaperVersions.Add(v);
        p.CurrentVersion = v.VersionNumber;
        if (p.Status == PaperStatus.Expected) p.Status = PaperStatus.Submitted;
        await _db.SaveChangesAsync(ct);
        return v;
    }

    public async Task<QuestionPaper> AdvanceAsync(int paperId, PaperStatus to, CancellationToken ct = default)
    {
        var p = await _db.QuestionPapers.FirstOrDefaultAsync(x => x.Id == paperId, ct)
                ?? throw new InvalidOperationException("Paper not found.");

        if (to == PaperStatus.Withdrawn)
            throw new InvalidOperationException("Use Withdraw (it needs a reason).");
        if (to == PaperStatus.Destroyed && p.ReturnedUtc is null)
            throw new InvalidOperationException("Record the return of unused copies before destruction.");
        if (to == PaperStatus.Approved && p.ExternalReviewRequired && p.Status < PaperStatus.ExternalReview)
            throw new InvalidOperationException("External review is required before approval.");
        if (p.CurrentVersion == 0 && to >= PaperStatus.UnderVetting)
            throw new InvalidOperationException("No version has been submitted yet.");
        if (to <= p.Status)
            throw new InvalidOperationException("Status can only move forward.");

        p.Status = to;
        await _db.SaveChangesAsync(ct);
        return p;
    }

    public async Task<PrintRun> RecordPrintRunAsync(int paperId, int copies, int spares, string? location, string? storageRef, CancellationToken ct = default)
    {
        var p = await _db.QuestionPapers.FirstOrDefaultAsync(x => x.Id == paperId, ct)
                ?? throw new InvalidOperationException("Paper not found.");
        if (p.Status < PaperStatus.Released)
            throw new InvalidOperationException("A paper must be Released before printing.");
        var run = new PrintRun { QuestionPaperId = paperId, Copies = copies, SpareCopies = spares, Location = location, SecureStorageRef = storageRef };
        _db.PrintRuns.Add(run);
        if (p.Status < PaperStatus.Printed) p.Status = PaperStatus.Printed;
        await _db.SaveChangesAsync(ct);
        return run;
    }

    public async Task DispatchAsync(int printRunId, CancellationToken ct = default)
    {
        var run = await _db.PrintRuns.Include(r => r.QuestionPaper).FirstOrDefaultAsync(r => r.Id == printRunId, ct)
                  ?? throw new InvalidOperationException("Print run not found.");
        run.DispatchedUtc = DateTimeOffset.UtcNow;
        if (run.QuestionPaper.Status < PaperStatus.Dispatched) run.QuestionPaper.Status = PaperStatus.Dispatched;
        await _db.SaveChangesAsync(ct);
    }

    public async Task RecordReturnAsync(int printRunId, int returnedUnused, CancellationToken ct = default)
    {
        var run = await _db.PrintRuns.Include(r => r.QuestionPaper).FirstOrDefaultAsync(r => r.Id == printRunId, ct)
                  ?? throw new InvalidOperationException("Print run not found.");
        run.ReturnedUnused = returnedUnused;
        run.QuestionPaper.ReturnedUtc = DateTimeOffset.UtcNow;
        if (run.QuestionPaper.Status < PaperStatus.Returned) run.QuestionPaper.Status = PaperStatus.Returned;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DestroyAsync(int printRunId, string witness, CancellationToken ct = default)
    {
        var run = await _db.PrintRuns.Include(r => r.QuestionPaper).FirstOrDefaultAsync(r => r.Id == printRunId, ct)
                  ?? throw new InvalidOperationException("Print run not found.");
        if (run.QuestionPaper.ReturnedUtc is null)
            throw new InvalidOperationException("Record the return before witnessed destruction.");
        run.DestroyedUtc = DateTimeOffset.UtcNow; run.DestructionWitness = witness;
        run.QuestionPaper.Status = PaperStatus.Destroyed;
        await _db.SaveChangesAsync(ct);
    }

    public async Task WithdrawAsync(int paperId, string reason, CancellationToken ct = default)
    {
        var p = await _db.QuestionPapers.FirstOrDefaultAsync(x => x.Id == paperId, ct)
                ?? throw new InvalidOperationException("Paper not found.");
        p.Status = PaperStatus.Withdrawn; p.WithdrawnReason = reason;
        await _db.SaveChangesAsync(ct);
    }
}
