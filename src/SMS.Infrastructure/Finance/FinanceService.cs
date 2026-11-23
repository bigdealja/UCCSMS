using Microsoft.EntityFrameworkCore;
using SMS.Application.Accounting;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Finance;

public record StatementLine(int Id, DateTimeOffset Date, string Type, string? Description,
                            decimal Amount, decimal RunningBalance, string AccountingStatus, string? AccountingRef);
public record Statement(int StudentId, decimal Balance, IReadOnlyList<StatementLine> Lines);

/// <summary>Student finance: post charges, record payments/refunds, produce statements, and
/// push each entry to whatever accounting system is configured — via IAccountingProvider,
/// so the vendor can change without touching this code.</summary>
public class FinanceService
{
    private readonly SmsDbContext _db;
    private readonly IAccountingProvider _accounting;
    public FinanceService(SmsDbContext db, IAccountingProvider accounting) { _db = db; _accounting = accounting; }

    public async Task<FinancialTransaction> PostChargeAsync(int studentId, int? termId, int? feeTypeId,
        decimal amount, string? description, CancellationToken ct = default)
    {
        if (amount <= 0) throw new InvalidOperationException("Charge amount must be positive.");
        var feeType = feeTypeId is int f ? await _db.FeeTypes.FindAsync(new object[] { f }, ct) : null;
        var tx = new FinancialTransaction
        {
            StudentId = studentId, TermId = termId, FeeTypeId = feeTypeId,
            Type = TransactionType.Charge, Amount = amount,          // charges positive
            Description = description ?? feeType?.Name
        };
        await PersistAndPostAsync(tx, feeType, ct);
        return tx;
    }

    public async Task<FinancialTransaction> RecordPaymentAsync(int studentId, decimal amount,
        PaymentMethod method, string? reference, string? description, bool isRefund = false, CancellationToken ct = default)
    {
        if (amount <= 0) throw new InvalidOperationException("Amount must be positive.");
        var tx = new FinancialTransaction
        {
            StudentId = studentId,
            Type = isRefund ? TransactionType.Refund : TransactionType.Payment,
            // Payments reduce the balance (-); refunds reverse a payment, raising it again (+).
            Amount = isRefund ? amount : -amount,
            Method = method, Reference = reference, Description = description
        };
        await PersistAndPostAsync(tx, null, ct);
        return tx;
    }

    async Task PersistAndPostAsync(FinancialTransaction tx, FeeType? feeType, CancellationToken ct)
    {
        _db.FinancialTransactions.Add(tx);
        await _db.SaveChangesAsync(ct);

        tx.AccountingProvider = _accounting.Name;
        if (!_accounting.IsExternal)
        {
            tx.AccountingStatus = AccountingSyncStatus.NotApplicable;
        }
        else
        {
            var student = await _db.Students.AsNoTracking().FirstAsync(s => s.Id == tx.StudentId, ct);
            var req = new AccountingPostRequest(student.StudentNumber, $"{student.FirstName} {student.LastName}",
                tx.Type.ToString(), Math.Abs(tx.Amount), feeType?.Code, feeType?.DistributionCode,
                tx.Reference, tx.Description, tx.PostedUtc);
            var result = await _accounting.PostAsync(req, ct);
            tx.AccountingStatus = result.Success ? AccountingSyncStatus.Posted : AccountingSyncStatus.Failed;
            tx.AccountingRef = result.ExternalRef;
            tx.AccountingError = result.Error;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<decimal> GetBalanceAsync(int studentId, CancellationToken ct = default)
        => await _db.FinancialTransactions.Where(t => t.StudentId == studentId).SumAsync(t => t.Amount, ct);

    public async Task<Statement> GetStatementAsync(int studentId, CancellationToken ct = default)
    {
        var txns = await _db.FinancialTransactions.AsNoTracking()
            .Where(t => t.StudentId == studentId)
            .OrderBy(t => t.PostedUtc).ThenBy(t => t.Id).ToListAsync(ct);
        decimal running = 0;
        var lines = txns.Select(t =>
        {
            running += t.Amount;
            return new StatementLine(t.Id, t.PostedUtc, t.Type.ToString(), t.Description, t.Amount, running,
                t.AccountingStatus.ToString(), t.AccountingRef);
        }).ToList();
        return new Statement(studentId, running, lines);
    }
}
