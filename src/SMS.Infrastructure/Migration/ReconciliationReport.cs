using System.Text;

namespace SMS.Infrastructure.DataMigration;

public class EntityResult
{
    public string Entity { get; init; } = default!;
    public int Source { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public List<string> Errors { get; } = new();
    public int Loaded => Inserted + Updated;
}

/// <summary>The reconciliation report for a migration run (EMS brief: controlled imports with a full
/// reconciliation report). Shows, per entity, source vs loaded counts and any errors.</summary>
public class ReconciliationReport
{
    public bool DryRun { get; init; }
    public List<EntityResult> Results { get; } = new();
    public EntityResult For(string entity)
    {
        var r = Results.FirstOrDefault(x => x.Entity == entity);
        if (r is null) { r = new EntityResult { Entity = entity }; Results.Add(r); }
        return r;
    }
    public bool Reconciled => Results.All(r => r.Source == r.Loaded + r.Skipped && r.Errors.Count == 0);

    public string ToText()
    {
        var sb = new StringBuilder();
        sb.AppendLine(DryRun ? "=== MIGRATION RECONCILIATION (DRY RUN) ===" : "=== MIGRATION RECONCILIATION ===");
        sb.AppendLine($"{"Entity",-14}{"Source",8}{"Inserted",10}{"Updated",9}{"Skipped",9}{"Errors",8}");
        foreach (var r in Results)
            sb.AppendLine($"{r.Entity,-14}{r.Source,8}{r.Inserted,10}{r.Updated,9}{r.Skipped,9}{r.Errors.Count,8}");
        sb.AppendLine($"Reconciled: {(Reconciled ? "YES" : "NO — review errors/skips")}");
        foreach (var r in Results.Where(r => r.Errors.Count > 0))
        {
            sb.AppendLine($"\n{r.Entity} errors:");
            foreach (var e in r.Errors.Take(50)) sb.AppendLine("  - " + e);
        }
        return sb.ToString();
    }
}
