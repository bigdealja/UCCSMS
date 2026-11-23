using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Grading;

public record PrePublicationIssue(string Kind, string Description, int EnrollmentId, int StudentId);
public record PrePublicationReport(int TermId, bool ReadyToPublish, IReadOnlyList<PrePublicationIssue> Issues);

/// <summary>Grade governance: a grade-change register with reason codes and second-person approval
/// (original values are preserved, never overwritten), withholding grades pending integrity cases,
/// and a pre-publication reconciliation check for a term.</summary>
public class GradeGovernanceService
{
    private readonly SmsDbContext _db;
    public GradeGovernanceService(SmsDbContext db) => _db = db;

    public async Task<GradeChangeRequest> ProposeAsync(int enrollmentId, string? proposedLetter, double? proposedMark,
        GradeChangeReasonCode reasonCode, string? reason, string proposedBy, CancellationToken ct = default)
    {
        var e = await _db.Enrollments.FirstOrDefaultAsync(x => x.Id == enrollmentId, ct)
                ?? throw new InvalidOperationException("Enrollment not found.");
        var req = new GradeChangeRequest
        {
            EnrollmentId = enrollmentId,
            OriginalLetterGrade = e.LetterGrade, OriginalFinalMark = e.FinalExamMark,
            ProposedLetterGrade = proposedLetter, ProposedFinalMark = proposedMark,
            ReasonCode = reasonCode, Reason = reason, ProposedBy = proposedBy
        };
        _db.GradeChangeRequests.Add(req);
        await _db.SaveChangesAsync(ct);
        return req;
    }

    /// <summary>Approve a change. The approver must not be the proposer (second-person check).
    /// On approval the enrollment grade is updated and the change is marked Applied; the original
    /// values remain on the request for the record.</summary>
    public async Task<GradeChangeRequest> ApproveAndApplyAsync(int requestId, string approver, CancellationToken ct = default)
    {
        var req = await _db.GradeChangeRequests.Include(r => r.Enrollment).FirstOrDefaultAsync(r => r.Id == requestId, ct)
                  ?? throw new InvalidOperationException("Request not found.");
        if (req.Status != GradeChangeStatus.Proposed)
            throw new InvalidOperationException("Only a proposed change can be approved.");
        if (string.Equals(approver, req.ProposedBy, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A grade change must be approved by someone other than the proposer.");

        var e = req.Enrollment;
        var batch = Guid.NewGuid();
        e.LetterGrade = req.ProposedLetterGrade;
        e.FinalExamMark = req.ProposedFinalMark;
        e.GradeSource = "GradeChange";
        e.GradedUtc = DateTimeOffset.UtcNow;
        e.ImportBatchId = batch;

        req.Status = GradeChangeStatus.Applied; req.DecidedBy = approver; req.DecidedUtc = DateTimeOffset.UtcNow; req.ApplyBatchId = batch;
        await _db.SaveChangesAsync(ct);
        return req;
    }

    public async Task RejectAsync(int requestId, string approver, string? reason, CancellationToken ct = default)
    {
        var req = await _db.GradeChangeRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct)
                  ?? throw new InvalidOperationException("Request not found.");
        req.Status = GradeChangeStatus.Rejected; req.DecidedBy = approver; req.DecidedUtc = DateTimeOffset.UtcNow;
        if (reason is not null) req.Reason = (req.Reason + " | Rejected: " + reason).Trim('|', ' ');
        await _db.SaveChangesAsync(ct);
    }

    public async Task WithholdAsync(int enrollmentId, int? caseId, CancellationToken ct = default)
    {
        var e = await _db.Enrollments.FirstOrDefaultAsync(x => x.Id == enrollmentId, ct)
                ?? throw new InvalidOperationException("Enrollment not found.");
        e.IsWithheld = true; e.WithheldCaseId = caseId;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ReleaseWithheldAsync(int enrollmentId, CancellationToken ct = default)
    {
        var e = await _db.Enrollments.FirstOrDefaultAsync(x => x.Id == enrollmentId, ct)
                ?? throw new InvalidOperationException("Enrollment not found.");
        e.IsWithheld = false; e.WithheldCaseId = null;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Reconciliation before publishing a term's grades: flags ungraded enrolments,
    /// withheld grades, and pending grade-change requests.</summary>
    public async Task<PrePublicationReport> PrePublicationCheckAsync(int termId, CancellationToken ct = default)
    {
        var enrolments = await _db.Enrollments.AsNoTracking()
            .Where(e => e.TermId == termId && e.Status == EnrollmentStatus.Registered)
            .Select(e => new { e.Id, e.StudentId, e.LetterGrade, e.IsWithheld }).ToListAsync(ct);

        var pendingIds = (await _db.GradeChangeRequests.AsNoTracking()
            .Where(r => r.Status == GradeChangeStatus.Proposed).Select(r => r.EnrollmentId).ToListAsync(ct)).ToHashSet();

        var issues = new List<PrePublicationIssue>();
        foreach (var e in enrolments)
        {
            if (string.IsNullOrEmpty(e.LetterGrade))
                issues.Add(new PrePublicationIssue("Ungraded", "No grade recorded.", e.Id, e.StudentId));
            if (e.IsWithheld)
                issues.Add(new PrePublicationIssue("Withheld", "Grade withheld (pending case).", e.Id, e.StudentId));
            if (pendingIds.Contains(e.Id))
                issues.Add(new PrePublicationIssue("PendingChange", "An unapproved grade-change request exists.", e.Id, e.StudentId));
        }
        return new PrePublicationReport(termId, issues.Count == 0, issues);
    }
}
