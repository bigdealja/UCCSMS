namespace SMS.Application.Accounting;

/// <summary>What the SMS sends to an external accounting system when a charge/payment is posted.
/// Deliberately generic — no vendor concepts — so any provider can map it to its own API.</summary>
public record AccountingPostRequest(
    string StudentNumber,
    string StudentName,
    string Kind,              // "Charge" | "Payment" | "Refund" | "Adjustment"
    decimal Amount,
    string? FeeCode,
    string? DistributionCode,
    string? Reference,
    string? Description,
    DateTimeOffset Date);

/// <summary>Result of posting to an external system.</summary>
public record AccountingPostResult(bool Success, string? ExternalRef, string? Error)
{
    public static AccountingPostResult Ok(string? externalRef) => new(true, externalRef, null);
    public static AccountingPostResult Fail(string error) => new(false, null, error);
}

/// <summary>Pluggable accounting integration. The finance module depends ONLY on this interface,
/// never on a specific vendor. Swap Sage 300 for QuickBooks, Xero, etc. by adding a new
/// implementation and selecting it via configuration — no change to finance logic.</summary>
public interface IAccountingProvider
{
    /// <summary>Provider name, e.g. "Sage300", "QuickBooks", "None". Stored on each transaction.</summary>
    string Name { get; }
    /// <summary>Whether this provider actually talks to an external system (false for the no-op).</summary>
    bool IsExternal { get; }
    Task<bool> HealthAsync(CancellationToken ct = default);
    Task<AccountingPostResult> PostAsync(AccountingPostRequest request, CancellationToken ct = default);
}
