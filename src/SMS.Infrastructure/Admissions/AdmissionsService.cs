using Microsoft.EntityFrameworkCore;
using SMS.Application.Crm;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Admissions;

/// <summary>Drives the admissions lifecycle per the UCC Admissions Policy: capture leads,
/// start/submit applications (auto Offer of Admission on submit), record decisions with
/// second-person approval, capture the offer response, and convert an accepted applicant
/// into a Student — linking the admissions funnel to registration.</summary>
public class AdmissionsService
{
    private readonly SmsDbContext _db;
    private readonly ICrmProvider _crm;
    public AdmissionsService(SmsDbContext db, ICrmProvider crm) { _db = db; _crm = crm; }

    // ---- CRM / leads ----
    public async Task<Lead> CreateLeadAsync(Lead lead, CancellationToken ct = default)
    {
        _db.Leads.Add(lead);
        await _db.SaveChangesAsync(ct);
        if (_crm.IsExternal)
        {
            var r = await _crm.UpsertLeadAsync(new CrmLead(lead.FirstName, lead.LastName, lead.Email, lead.Phone, lead.Source, lead.Status.ToString()), ct);
            if (r.Success && r.ExternalId is not null) { lead.ExternalCrmId = r.ExternalId; await _db.SaveChangesAsync(ct); }
        }
        return lead;
    }

    public async Task<AdmissionApplication> ConvertLeadToApplicationAsync(int leadId, int programmeId, int intakeTermId, CancellationToken ct = default)
    {
        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == leadId, ct)
                   ?? throw new InvalidOperationException($"Lead {leadId} not found.");
        var app = new AdmissionApplication
        {
            LeadId = lead.Id, FirstName = lead.FirstName, LastName = lead.LastName, Email = lead.Email,
            ProgrammeId = programmeId, IntakeTermId = intakeTermId,
            ApplicationNumber = $"APP-{DateTime.UtcNow:yyyy}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}"
        };
        _db.Applications.Add(app);
        lead.Status = LeadStatus.ApplicationStarted;
        await _db.SaveChangesAsync(ct);
        return app;
    }

    // ---- AdmissionApplication lifecycle ----
    public async Task SubmitAsync(int applicationId, CancellationToken ct = default)
    {
        var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == applicationId, ct)
                  ?? throw new InvalidOperationException("AdmissionApplication not found.");
        if (app.Status != ApplicationStatus.Draft)
            throw new InvalidOperationException("Only a draft application can be submitted.");
        app.Status = ApplicationStatus.Submitted;          // policy: automatic Offer of Admission on submission
        app.SubmittedUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task StartReviewAsync(int applicationId, string officer, CancellationToken ct = default)
    {
        var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == applicationId, ct)
                  ?? throw new InvalidOperationException("AdmissionApplication not found.");
        app.AssignedOfficer = officer;
        app.Status = ApplicationStatus.UnderReview;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Record the admission decision. Enforces segregation of duties: the decider must
    /// not be the assigned officer (policy / EMS "nobody approves their own work").</summary>
    public async Task<AdmissionApplication> RecordDecisionAsync(int applicationId, AdmissionCategory category,
        string decidedBy, string? notes, CancellationToken ct = default)
    {
        var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == applicationId, ct)
                  ?? throw new InvalidOperationException("AdmissionApplication not found.");
        if (category == AdmissionCategory.None)
            throw new InvalidOperationException("A decision category is required.");
        if (!string.IsNullOrEmpty(app.AssignedOfficer) &&
            string.Equals(app.AssignedOfficer, decidedBy, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The reviewing officer cannot approve their own decision (second-person approval required).");

        app.Decision = category;
        app.DecidedBy = decidedBy;
        app.DecidedUtc = DateTimeOffset.UtcNow;
        app.DecisionNotes = notes;
        app.Status = ApplicationStatus.Accepted;   // any category here is an accept-type offer; Declined handled separately
        await _db.SaveChangesAsync(ct);
        return app;
    }

    public async Task DeclineAsync(int applicationId, string decidedBy, string? notes, CancellationToken ct = default)
    {
        var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == applicationId, ct)
                  ?? throw new InvalidOperationException("AdmissionApplication not found.");
        app.Status = ApplicationStatus.Declined; app.DecidedBy = decidedBy; app.DecidedUtc = DateTimeOffset.UtcNow; app.DecisionNotes = notes;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Placement Offer Response Form: the applicant accepts / declines / defers.</summary>
    public async Task RecordOfferResponseAsync(int applicationId, OfferResponse response, CancellationToken ct = default)
    {
        var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == applicationId, ct)
                  ?? throw new InvalidOperationException("AdmissionApplication not found.");
        app.OfferResponse = response;
        if (response == OfferResponse.Deferred) app.Status = ApplicationStatus.Deferred;
        if (response == OfferResponse.Declined) app.Status = ApplicationStatus.Declined;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Convert an accepted applicant into a Student — creates the record the Registration
    /// module uses, completing the lead → application → admission → student lifecycle.</summary>
    public async Task<Student> ConvertToStudentAsync(int applicationId, string studentNumber, CancellationToken ct = default)
    {
        var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == applicationId, ct)
                  ?? throw new InvalidOperationException("AdmissionApplication not found.");
        if (app.Status != ApplicationStatus.Accepted || app.Decision == AdmissionCategory.None)
            throw new InvalidOperationException("Only an accepted application can be converted to a student.");
        if (app.StudentId is not null)
            throw new InvalidOperationException("This application already has a student record.");
        if (await _db.Students.AnyAsync(s => s.StudentNumber == studentNumber, ct))
            throw new InvalidOperationException($"Student number '{studentNumber}' already exists.");

        var student = new Student
        {
            StudentNumber = studentNumber, FirstName = app.FirstName, LastName = app.LastName,
            Email = app.Email, DateOfBirth = app.DateOfBirth, ProgrammeId = app.ProgrammeId,
            Status = StudentStatus.Accepted
        };
        _db.Students.Add(student);
        await _db.SaveChangesAsync(ct);

        app.StudentId = student.Id;
        app.Status = ApplicationStatus.Enrolled;
        if (app.LeadId is int lid)
        {
            var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == lid, ct);
            if (lead is not null) lead.Status = LeadStatus.Converted;
        }
        await _db.SaveChangesAsync(ct);
        return student;
    }
}
