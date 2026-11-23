namespace SMS.Application.Accounting;

/// <summary>Chooses and configures the accounting integration. Default "None" means the SMS keeps
/// its own ledger and posts nowhere external until a provider is configured.</summary>
public class AccountingOptions
{
    public string Provider { get; set; } = "None";   // "None" | "Sage300" | "QuickBooks" | "Xero" | …
    public Sage300Options Sage300 { get; set; } = new();
    // Future providers add their own options sections here (QuickBooks, Xero, …).
}

public class Sage300Options
{
    public string BaseUrl { get; set; } = "";   // HTTPS only, e.g. https://erp.your-institution.edu/Sage300WebApi
    public string Company { get; set; } = "";    // e.g. UCCDAT
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}
