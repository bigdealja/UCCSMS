using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Notifications;

/// <summary>Creates and serves notifications. CreateIfAbsent dedupes by (Category, RefType, RefId)
/// against still-unread notifications, so re-running alert generation does not spam the inbox.</summary>
public class NotificationService
{
    private readonly SmsDbContext _db;
    public NotificationService(SmsDbContext db) => _db = db;

    public async Task<Notification?> CreateIfAbsentAsync(string title, string? body, string category,
        NotificationSeverity severity, string? refType, int? refId, string? recipient = null, CancellationToken ct = default)
    {
        bool exists = await _db.Notifications.AnyAsync(n =>
            !n.IsRead && n.Category == category && n.RefType == refType && n.RefId == refId && n.Recipient == recipient, ct);
        if (exists) return null;
        var n = new Notification { Title = title, Body = body, Category = category, Severity = severity,
            RefType = refType, RefId = refId, Recipient = recipient };
        _db.Notifications.Add(n);
        await _db.SaveChangesAsync(ct);
        return n;
    }

    public async Task<List<Notification>> InboxAsync(string? recipient, bool unreadOnly, CancellationToken ct = default)
    {
        var q = _db.Notifications.AsNoTracking()
            .Where(n => n.Recipient == null || n.Recipient == recipient);   // personal + broadcast
        if (unreadOnly) q = q.Where(n => !n.IsRead);
        return await q.OrderByDescending(n => n.CreatedUtc).Take(200).ToListAsync(ct);
    }

    public async Task MarkReadAsync(int id, CancellationToken ct = default)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (n is null || n.IsRead) return;
        n.IsRead = true; n.ReadUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
