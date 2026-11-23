using SMS.Domain.Common;
using SMS.Domain.Enums;
namespace SMS.Domain.Entities;

/// <summary>A single entry on a student's financial ledger. Balance is the sum of Amount
/// across a student's transactions (charges positive, payments/refunds negative).</summary>
public class FinancialTransaction : BaseEntity
{
    public int StudentId { get; set; }
    public Student Student { get; set; } = default!;
    public int? TermId { get; set; }
    public Term? Term { get; set; }
    public int? FeeTypeId { get; set; }
    public FeeType? FeeType { get; set; }

    public TransactionType Type { get; set; }
    /// <summary>Signed amount: + for charges, - for payments/refunds. Service enforces the sign.</summary>
    public decimal Amount { get; set; }
    public PaymentMethod? Method { get; set; }
    public string? Reference { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset PostedUtc { get; set; } = DateTimeOffset.UtcNow;

    // External accounting integration (provider-agnostic)
    public AccountingSyncStatus AccountingStatus { get; set; } = AccountingSyncStatus.NotApplicable;
    public string? AccountingProvider { get; set; }   // which system: "Sage300", "QuickBooks", …
    public string? AccountingRef { get; set; }         // external document number returned by that system
    public string? AccountingError { get; set; }
}
