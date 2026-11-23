using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SMS.Infrastructure.Persistence;

/// <summary>Design-time factory so `dotnet ef migrations add` works without the API running.
/// Uses a placeholder connection string — real one comes from configuration at runtime.</summary>
public class SmsDbContextFactory : IDesignTimeDbContextFactory<SmsDbContext>
{
    public SmsDbContext CreateDbContext(string[] args)
    {
        var opts = new DbContextOptionsBuilder<SmsDbContext>()
            .UseSqlServer("Server=.;Database=SMS;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new SmsDbContext(opts);
    }
}
