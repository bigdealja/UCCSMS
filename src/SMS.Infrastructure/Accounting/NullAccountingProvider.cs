using SMS.Application.Accounting;

namespace SMS.Infrastructure.Accounting;

/// <summary>Default no-op provider. The SMS keeps its own ledger and posts nowhere external.
/// Used until an institution configures a real accounting system.</summary>
public class NullAccountingProvider : IAccountingProvider
{
    public string Name => "None";
    public bool IsExternal => false;
    public Task<bool> HealthAsync(CancellationToken ct = default) => Task.FromResult(true);
    public Task<AccountingPostResult> PostAsync(AccountingPostRequest request, CancellationToken ct = default)
        => Task.FromResult(AccountingPostResult.Ok(null));
}
