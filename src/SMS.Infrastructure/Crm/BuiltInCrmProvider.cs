using SMS.Application.Crm;

namespace SMS.Infrastructure.Crm;

/// <summary>Default: the built-in CRM. Leads live in our own database; nothing external.</summary>
public class BuiltInCrmProvider : ICrmProvider
{
    public string Name => "BuiltIn";
    public bool IsExternal => false;
    public Task<bool> HealthAsync(CancellationToken ct = default) => Task.FromResult(true);
    public Task<CrmUpsertResult> UpsertLeadAsync(CrmLead lead, CancellationToken ct = default)
        => Task.FromResult(new CrmUpsertResult(true, null, null));
}
