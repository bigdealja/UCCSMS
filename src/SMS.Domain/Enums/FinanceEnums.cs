namespace SMS.Domain.Enums;

/// <summary>Signed ledger entry types. Charges increase what a student owes; payments/refunds/
/// adjustments move the balance the other way (sign handled in the service).</summary>
public enum TransactionType { Charge, Payment, Refund, Adjustment }
public enum PaymentMethod { Cash, Card, BankTransfer, Cheque, Scholarship, Waiver, Other }

/// <summary>How far a ledger entry has got with the external accounting system.</summary>
public enum AccountingSyncStatus { NotApplicable, Pending, Posted, Failed }
