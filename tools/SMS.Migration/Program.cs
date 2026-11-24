using Microsoft.EntityFrameworkCore;
using SMS.Infrastructure.DataMigration;
using SMS.Infrastructure.Persistence;

// Aeorion -> new SMS data migration tool.
//
// Env:
//   SMS_TARGET_DB   target (new) SQL Server connection string  [required]
//   SMS_LEGACY_DB   legacy Aeorion SQL Server connection string
//   SMS_LEGACY_CSV  directory of exported CSVs (used if SMS_LEGACY_DB is not set)
// Args:
//   --apply         actually write (default is a DRY RUN with reconciliation only)

bool apply = args.Contains("--apply");
var target = Environment.GetEnvironmentVariable("SMS_TARGET_DB");
if (string.IsNullOrWhiteSpace(target)) { Console.Error.WriteLine("Set SMS_TARGET_DB (new database connection string)."); return 1; }

ILegacySource source;
var legacyDb = Environment.GetEnvironmentVariable("SMS_LEGACY_DB");
var legacyCsv = Environment.GetEnvironmentVariable("SMS_LEGACY_CSV");
if (!string.IsNullOrWhiteSpace(legacyDb)) { source = new SqlLegacySource(legacyDb); Console.WriteLine("Source: Aeorion SQL Server"); }
else if (!string.IsNullOrWhiteSpace(legacyCsv)) { source = new CsvLegacySource(legacyCsv); Console.WriteLine($"Source: CSV export ({legacyCsv})"); }
else { Console.Error.WriteLine("Set SMS_LEGACY_DB or SMS_LEGACY_CSV."); return 1; }

var options = new DbContextOptionsBuilder<SmsDbContext>().UseSqlServer(target).Options;
using var db = new SmsDbContext(options);

Console.WriteLine(apply ? "Mode: APPLY (writing changes)" : "Mode: DRY RUN (no changes written)");
var report = await new MigrationRunner(db, source).RunAsync(dryRun: !apply);
Console.WriteLine();
Console.WriteLine(report.ToText());
return report.Reconciled ? 0 : 2;
