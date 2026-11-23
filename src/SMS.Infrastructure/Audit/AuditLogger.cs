using SMS.Domain.Entities;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Audit;

/// <summary>Explicit logging of reads/downloads of restricted records (the change interceptor only
/// covers writes).</summary>
public interface IAuditLogger
{
    Task LogAccessAsync(string action, string entityType, string entityId, string? reason = null, CancellationToken ct = default);
}

public sealed class AuditLogger : IAuditLogger
{
    private readonly SmsDbContext _db;
    private readonly ICurrentUserAccessor _user;
    public AuditLogger(SmsDbContext db, ICurrentUserAccessor user) { _db = db; _user = user; }

    public async Task LogAccessAsync(string action, string entityType, string entityId, string? reason = null, CancellationToken ct = default)
    {
        _db.Set<AuditEvent>().Add(new AuditEvent { Actor = _user.UserName ?? "system", Action = action,
            EntityType = entityType, EntityId = entityId, Reason = reason });
        await _db.SaveChangesAsync(ct);
    }
}
