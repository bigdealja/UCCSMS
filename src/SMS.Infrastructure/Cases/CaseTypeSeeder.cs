using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Cases;

/// <summary>Seeds the 14 case types from the EMS brief. Turnaround days + second-person approval
/// are starting values, editable afterwards as configuration.</summary>
public static class CaseTypeSeeder
{
    public static async Task EnsureSeededAsync(SmsDbContext db, CancellationToken ct = default)
    {
        if (await db.CaseTypes.IgnoreQueryFilters().AnyAsync(ct)) return;
        // Code, Name, turnaround working days, requires second-person approval
        (string, string, int, bool)[] types =
        {
            ("QRY", "Query", 5, false),
            ("GRAMD", "Grade Amendment", 10, true),
            ("ADRP", "Add/Drop", 3, false),
            ("ALT", "Alternative Request", 5, false),
            ("RSIT", "Resit Request", 10, true),
            ("CLSH", "Clash Form", 3, false),
            ("EEREC", "EE Recruitment / Contract", 15, true),
            ("IRREG", "Irregularity", 20, true),
            ("CWBR", "Coursework Breach", 20, true),
            ("APPL", "Appeal", 20, true),
            ("CMTE", "Committee Action Item", 10, false),
            ("SRMK", "Script Remark", 10, true),
            ("SREV", "Script Review", 10, false),
            ("CXLOC", "Change of Exam Location", 3, false),
        };
        foreach (var (code, name, days, approval) in types)
            db.CaseTypes.Add(new CaseType { Code = code, Name = name, TurnaroundWorkingDays = days, RequiresSecondPersonApproval = approval });
        await db.SaveChangesAsync(ct);
    }
}
