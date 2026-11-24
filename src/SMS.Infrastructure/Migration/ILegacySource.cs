namespace SMS.Infrastructure.DataMigration;

/// <summary>A source of legacy Aeorion data to migrate. Implemented over the live SQL Server, or
/// over exported CSV files (so migration can run offline / before direct DB access is arranged).</summary>
public interface ILegacySource
{
    IReadOnlyList<LegacyCampus> Campuses();
    IReadOnlyList<LegacySchool> Schools();
    IReadOnlyList<LegacyProgramme> Programmes();
    IReadOnlyList<LegacyTerm> Terms();
    IReadOnlyList<LegacyCourse> Courses();
    IReadOnlyList<LegacyStudent> Students();
    IReadOnlyList<LegacyEnrollment> Enrollments();

    // Optional historical data. Default to empty so a source only implements what it exports.
    IReadOnlyList<LegacyFeeType> FeeTypes() => Array.Empty<LegacyFeeType>();
    IReadOnlyList<LegacyFinanceTxn> FinanceTransactions() => Array.Empty<LegacyFinanceTxn>();
    IReadOnlyList<LegacyLead> Leads() => Array.Empty<LegacyLead>();
    IReadOnlyList<LegacyApplication> Applications() => Array.Empty<LegacyApplication>();
}
