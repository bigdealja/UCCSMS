using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SMS.Domain.Entities;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Audit;

/// <summary>Automatically records an append-only AuditEvent for every create/update/delete across
/// the system. Captures the changed entries while saving, then writes the audit rows (with final
/// keys) after the save, in the same context.</summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserAccessor _user;
    public AuditSaveChangesInterceptor(ICurrentUserAccessor user) => _user = user;

    // Entity types excluded from change auditing (noise / infra).
    static readonly HashSet<string> Excluded = new() { nameof(AuditEvent), nameof(Notification), nameof(AssistantQueryLog) };

    private readonly List<(EntityEntry Entry, string Action, string? Before, string? After)> _pending = new();
    private bool _writing;

    public override InterceptionResult<int> SavingChanges(DbContextEventData e, InterceptionResult<int> r)
    { Capture(e.Context); return base.SavingChanges(e, r); }
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e, InterceptionResult<int> r, CancellationToken ct = default)
    { Capture(e.Context); return base.SavingChangesAsync(e, r, ct); }

    public override int SavedChanges(SaveChangesCompletedEventData e, int r)
    { Flush(e.Context).GetAwaiter().GetResult(); return base.SavedChanges(e, r); }
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData e, int r, CancellationToken ct = default)
    { await Flush(e.Context); return await base.SavedChangesAsync(e, r, ct); }

    void Capture(Microsoft.EntityFrameworkCore.DbContext? context)
    {
        if (_writing || context is not SmsDbContext) return;
        foreach (var entry in context.ChangeTracker.Entries())
        {
            var typeName = entry.Entity.GetType().Name;
            if (Excluded.Contains(typeName)) continue;
            string? action = entry.State switch
            {
                EntityState.Added => "Create",
                EntityState.Deleted => "Delete",
                EntityState.Modified => "Update",
                _ => null
            };
            if (action is null) continue;

            var before = new Dictionary<string, object?>();
            var after = new Dictionary<string, object?>();
            foreach (var p in entry.Properties)
            {
                if (entry.State == EntityState.Added) { after[p.Metadata.Name] = p.CurrentValue; }
                else if (entry.State == EntityState.Deleted) { before[p.Metadata.Name] = p.OriginalValue; }
                else if (p.IsModified) { before[p.Metadata.Name] = p.OriginalValue; after[p.Metadata.Name] = p.CurrentValue; }
            }
            // Soft-delete shows up as Modified IsDeleted=true -> record it as a Delete.
            if (entry.State == EntityState.Modified && after.ContainsKey("IsDeleted") && Equals(after["IsDeleted"], true))
                action = "Delete";

            _pending.Add((entry, action, before.Count == 0 ? null : Json(before), after.Count == 0 ? null : Json(after)));
        }
    }

    async Task Flush(Microsoft.EntityFrameworkCore.DbContext? context)
    {
        if (_writing || _pending.Count == 0 || context is not SmsDbContext ctx) { _pending.Clear(); return; }
        _writing = true;
        try
        {
            var actor = _user.UserName ?? "system";
            foreach (var (entry, action, before, after) in _pending)
            {
                string? id = null;
                var key = entry.Metadata.FindPrimaryKey();
                if (key is not null)
                    id = string.Join(",", key.Properties.Select(pp => entry.CurrentValues[pp]?.ToString()));
                ctx.Set<AuditEvent>().Add(new AuditEvent
                {
                    Actor = actor, Action = action, EntityType = entry.Entity.GetType().Name,
                    EntityId = id, Before = before, After = after
                });
            }
            _pending.Clear();
            await ctx.SaveChangesAsync();   // guarded by _writing so it doesn't re-audit
        }
        finally { _writing = false; }
    }

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }  // readable enum values
    };
    static string Json(Dictionary<string, object?> d) => JsonSerializer.Serialize(d, JsonOpts);
}
