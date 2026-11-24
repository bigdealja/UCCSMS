using Microsoft.EntityFrameworkCore;
using SMS.Application.Hr;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Hr;

/// <summary>Exam-work payment authorisation: dated rate tables, work claims computed from the
/// effective rate, second-person approval, and export to the configured HR/payroll provider.</summary>
public class PayrollService
{
    private readonly SmsDbContext _db;
    private readonly IHrProvider _hr;
    public PayrollService(SmsDbContext db, IHrProvider hr) { _db = db; _hr = hr; }

    public async Task<PayRate> AddRateAsync(WorkType type, decimal rate, string? unit, DateOnly from, CancellationToken ct = default)
    {
        var r = new PayRate { WorkType = type, RatePerUnit = rate, Unit = unit, EffectiveFrom = from };
        _db.PayRates.Add(r); await _db.SaveChangesAsync(ct);
        return r;
    }

    public async Task<PayRate?> GetEffectiveRateAsync(WorkType type, DateOnly on, CancellationToken ct = default)
        => await _db.PayRates.AsNoTracking().Where(r => r.WorkType == type && r.IsActive && r.EffectiveFrom <= on)
            .OrderByDescending(r => r.EffectiveFrom).FirstOrDefaultAsync(ct);

    public async Task<WorkClaim> CreateClaimAsync(string claimant, int? staffId, WorkType type, decimal units,
        DateOnly onDate, string? evidence, CancellationToken ct = default)
    {
        if (units <= 0) throw new InvalidOperationException("Units must be positive.");
        var rate = await GetEffectiveRateAsync(type, onDate, ct)
                   ?? throw new InvalidOperationException($"No effective rate for {type} on {onDate:yyyy-MM-dd}.");
        var claim = new WorkClaim { ClaimantName = claimant, StaffFacultyId = staffId, WorkType = type, Units = units,
            RatePerUnit = rate.RatePerUnit, Amount = units * rate.RatePerUnit, CompletionEvidence = evidence };
        _db.WorkClaims.Add(claim); await _db.SaveChangesAsync(ct);
        return claim;
    }

    public async Task SubmitAsync(int claimId, string by, CancellationToken ct = default)
    {
        var c = await Get(claimId, ct);
        if (c.Status != ClaimStatus.Draft) throw new InvalidOperationException("Only a draft claim can be submitted.");
        c.Status = ClaimStatus.Submitted; c.SubmittedBy = by; await _db.SaveChangesAsync(ct);
    }

    /// <summary>Approve a claim. The approver must not be the submitter (nobody approves their own).</summary>
    public async Task ApproveAsync(int claimId, string approver, CancellationToken ct = default)
    {
        var c = await Get(claimId, ct);
        if (c.Status != ClaimStatus.Submitted) throw new InvalidOperationException("Only a submitted claim can be approved.");
        if (string.Equals(approver, c.SubmittedBy, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A claim must be approved by someone other than the submitter.");
        c.Status = ClaimStatus.Approved; c.ApprovedBy = approver; await _db.SaveChangesAsync(ct);
    }

    public async Task RejectAsync(int claimId, string by, CancellationToken ct = default)
    { var c = await Get(claimId, ct); c.Status = ClaimStatus.Rejected; c.ApprovedBy = by; await _db.SaveChangesAsync(ct); }

    /// <summary>Export an approved claim to the HR/payroll provider.</summary>
    public async Task<WorkClaim> ExportAsync(int claimId, CancellationToken ct = default)
    {
        var c = await Get(claimId, ct);
        if (c.Status != ClaimStatus.Approved) throw new InvalidOperationException("Only an approved claim can be exported.");
        var result = await _hr.ExportAsync(new HrExportRequest(c.ClaimantName, c.WorkType.ToString(), c.Units, c.Amount, $"CLAIM-{c.Id}", DateOnly.FromDateTime(DateTime.UtcNow)), ct);
        if (!result.Success) throw new InvalidOperationException($"HR export failed: {result.Error}");
        c.Status = ClaimStatus.Exported; c.ExternalRef = result.ExternalRef; c.Provider = _hr.Name;
        await _db.SaveChangesAsync(ct);
        return c;
    }

    async Task<WorkClaim> Get(int id, CancellationToken ct)
        => await _db.WorkClaims.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new InvalidOperationException("Claim not found.");
}
