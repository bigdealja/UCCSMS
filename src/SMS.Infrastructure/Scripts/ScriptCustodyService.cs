using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Scripts;

/// <summary>Script bundle chain of custody: collect, hand over, scan, issue to markers, record
/// marked uploads, set retention, and dispose (witnessed, only after the retention date). Every
/// step appends a custody event.</summary>
public class ScriptCustodyService
{
    private readonly SmsDbContext _db;
    public ScriptCustodyService(SmsDbContext db) => _db = db;

    public async Task<ScriptBundle> CreateBundleAsync(int examId, string code, int scriptCount, string custodian, CancellationToken ct = default)
    {
        var b = new ScriptBundle { ExamId = examId, Code = code, ScriptCount = scriptCount, Custodian = custodian };
        _db.ScriptBundles.Add(b);
        await _db.SaveChangesAsync(ct);
        await LogAsync(b.Id, CustodyAction.Collected, null, custodian, custodian, $"{scriptCount} scripts collected", ct);
        return b;
    }

    public async Task HandoverAsync(int bundleId, string toCustodian, string by, CancellationToken ct = default)
    {
        var b = await Get(bundleId, ct);
        var from = b.Custodian; b.Custodian = toCustodian;
        await LogAsync(bundleId, CustodyAction.Handover, from, toCustodian, by, null, ct);
    }

    public async Task<ScanBatch> RecordScanAsync(int bundleId, int scannedCount, string? collectionRef, string by, CancellationToken ct = default)
    {
        var b = await Get(bundleId, ct);
        var batch = new ScanBatch { ScriptBundleId = bundleId, ScannedCount = scannedCount, CollectionRef = collectionRef, ScannedBy = by };
        _db.ScanBatches.Add(batch);
        if (b.Status < ScriptBundleStatus.Scanned) b.Status = ScriptBundleStatus.Scanned;
        await LogAsync(bundleId, CustodyAction.Scanned, null, null, by, $"{scannedCount} scanned (ref {collectionRef})", ct);
        return batch;
    }

    public async Task IssueToMarkerAsync(int bundleId, string marker, string by, CancellationToken ct = default)
    {
        var b = await Get(bundleId, ct);
        var from = b.Custodian; b.Custodian = marker; b.Status = ScriptBundleStatus.WithMarker;
        await LogAsync(bundleId, CustodyAction.IssuedToMarker, from, marker, by, null, ct);
    }

    public async Task<MarkedScriptUpload> UploadMarkedAsync(int bundleId, string fileName, int? documentId, string by, CancellationToken ct = default)
    {
        var b = await Get(bundleId, ct);
        var up = new MarkedScriptUpload { ScriptBundleId = bundleId, FileName = fileName, DocumentId = documentId, UploadedBy = by };
        _db.MarkedScriptUploads.Add(up);
        if (b.Status < ScriptBundleStatus.Marked) b.Status = ScriptBundleStatus.Marked;
        await LogAsync(bundleId, CustodyAction.MarkedUploaded, null, null, by, fileName, ct);
        return up;
    }

    public async Task ReturnAsync(int bundleId, string toCustodian, string by, CancellationToken ct = default)
    {
        var b = await Get(bundleId, ct);
        var from = b.Custodian; b.Custodian = toCustodian; b.Status = ScriptBundleStatus.Returned;
        await LogAsync(bundleId, CustodyAction.Returned, from, toCustodian, by, null, ct);
    }

    public async Task SetRetentionAsync(int bundleId, DateOnly until, string by, CancellationToken ct = default)
    {
        var b = await Get(bundleId, ct);
        b.RetentionUntil = until; b.Status = ScriptBundleStatus.Retained;
        await LogAsync(bundleId, CustodyAction.Retained, null, null, by, $"retain until {until}", ct);
    }

    public async Task DisposeAsync(int bundleId, string witness, string by, CancellationToken ct = default)
    {
        var b = await Get(bundleId, ct);
        if (b.RetentionUntil is null)
            throw new InvalidOperationException("Set a retention period before disposal.");
        if (DateOnly.FromDateTime(DateTime.UtcNow) < b.RetentionUntil.Value)
            throw new InvalidOperationException($"Cannot dispose before the retention date ({b.RetentionUntil:yyyy-MM-dd}).");
        b.Status = ScriptBundleStatus.Disposed; b.DisposedUtc = DateTimeOffset.UtcNow; b.DisposalWitness = witness;
        await LogAsync(bundleId, CustodyAction.Disposed, null, null, by, $"witnessed by {witness}", ct);
    }

    public async Task<List<CustodyEvent>> ChainAsync(int bundleId, CancellationToken ct = default)
        => await _db.CustodyEvents.AsNoTracking().Where(e => e.ScriptBundleId == bundleId)
            .OrderBy(e => e.CreatedUtc).ToListAsync(ct);

    async Task<ScriptBundle> Get(int id, CancellationToken ct)
        => await _db.ScriptBundles.FirstOrDefaultAsync(b => b.Id == id, ct) ?? throw new InvalidOperationException("Bundle not found.");

    async Task LogAsync(int bundleId, CustodyAction action, string? from, string? to, string? by, string? notes, CancellationToken ct)
    {
        _db.CustodyEvents.Add(new CustodyEvent { ScriptBundleId = bundleId, Action = action, FromCustodian = from, ToCustodian = to, By = by, Notes = notes });
        await _db.SaveChangesAsync(ct);
    }
}
