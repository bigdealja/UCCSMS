using Microsoft.EntityFrameworkCore;
using SMS.Domain.Common;
using SMS.Domain.Entities;

namespace SMS.Infrastructure.Persistence;

public class SmsDbContext : DbContext
{
    public SmsDbContext(DbContextOptions<SmsDbContext> options) : base(options) { }

    public DbSet<Campus> Campuses => Set<Campus>();
    public DbSet<School> Schools => Set<School>();
    public DbSet<Programme> Programmes => Set<Programme>();
    public DbSet<Term> Terms => Set<Term>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<StaffFaculty> StaffFaculty => Set<StaffFaculty>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<ExamQuestion> ExamQuestions => Set<ExamQuestion>();
    public DbSet<ExamResult> ExamResults => Set<ExamResult>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<ExamRoomAllocation> ExamRoomAllocations => Set<ExamRoomAllocation>();
    public DbSet<ExamInvigilator> ExamInvigilators => Set<ExamInvigilator>();
    public DbSet<SpecialArrangement> SpecialArrangements => Set<SpecialArrangement>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<FeeType> FeeTypes => Set<FeeType>();
    public DbSet<FinancialTransaction> FinancialTransactions => Set<FinancialTransaction>();
    public DbSet<GradeScheme> GradeSchemes => Set<GradeScheme>();
    public DbSet<GradeBand> GradeBands => Set<GradeBand>();
    public DbSet<GradeChangeRequest> GradeChangeRequests => Set<GradeChangeRequest>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<AdmissionApplication> Applications => Set<AdmissionApplication>();
    public DbSet<ApplicationDocument> ApplicationDocuments => Set<ApplicationDocument>();
    public DbSet<AdmissionRecommendation> AdmissionRecommendations => Set<AdmissionRecommendation>();
    public DbSet<CaseType> CaseTypes => Set<CaseType>();
    public DbSet<Case> Cases => Set<Case>();
    public DbSet<CaseAction> CaseActions => Set<CaseAction>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<KnowledgeArticle> KnowledgeArticles => Set<KnowledgeArticle>();
    public DbSet<AssistantQueryLog> AssistantQueryLogs => Set<AssistantQueryLog>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRoleAssignment> UserRoleAssignments => Set<UserRoleAssignment>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<ExternalExaminer> ExternalExaminers => Set<ExternalExaminer>();
    public DbSet<ExaminerAssignment> ExaminerAssignments => Set<ExaminerAssignment>();
    public DbSet<ExaminerReport> ExaminerReports => Set<ExaminerReport>();
    public DbSet<ExaminerPortalToken> ExaminerPortalTokens => Set<ExaminerPortalToken>();
    public DbSet<QuestionPaper> QuestionPapers => Set<QuestionPaper>();
    public DbSet<QuestionPaperVersion> QuestionPaperVersions => Set<QuestionPaperVersion>();
    public DbSet<PrintRun> PrintRuns => Set<PrintRun>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Campus>().HasIndex(x => x.Code).IsUnique();
        b.Entity<School>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Programme>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Course>().HasIndex(x => x.Code);
        b.Entity<Student>().HasIndex(x => x.StudentNumber).IsUnique();
        b.Entity<StaffFaculty>().HasIndex(x => x.StaffNumber).IsUnique();

        // A student takes a given course once per term
        b.Entity<Enrollment>().HasIndex(x => new { x.StudentId, x.CourseId, x.TermId }).IsUnique();
        b.Entity<Enrollment>().HasIndex(x => x.ImportBatchId);

        b.Entity<ExamResult>().HasIndex(x => new { x.ExamId, x.StudentId }).IsUnique();
        b.Entity<Room>().HasIndex(x => x.Code).IsUnique();
        b.Entity<ExamRoomAllocation>().HasIndex(x => new { x.ExamId, x.RoomId }).IsUnique();
        b.Entity<SpecialArrangement>().HasIndex(x => x.StudentId);
        b.Entity<ExamQuestion>().HasIndex(x => new { x.ExamId, x.QuestionId }).IsUnique();
        b.Entity<Document>().HasIndex(x => new { x.StudentId, x.Category });
        b.Entity<DocumentVersion>().HasIndex(x => new { x.DocumentId, x.VersionNumber }).IsUnique();
        b.Entity<FeeType>().HasIndex(x => x.Code).IsUnique();
        b.Entity<FinancialTransaction>().HasIndex(x => x.StudentId);
        b.Entity<FinancialTransaction>().Property(x => x.Amount).HasPrecision(18, 2);
        b.Entity<FeeType>().Property(x => x.DefaultAmount).HasPrecision(18, 2);
        b.Entity<GradeScheme>().HasIndex(x => new { x.Level, x.EffectiveFrom });
        b.Entity<GradeChangeRequest>().HasIndex(x => new { x.EnrollmentId, x.Status });
        b.Entity<AdmissionApplication>().HasIndex(x => x.ApplicationNumber).IsUnique();
        b.Entity<AdmissionApplication>().HasIndex(x => x.Status);
        b.Entity<Lead>().HasIndex(x => x.Status);

        // Don't cascade-delete across the academic graph
        foreach (var fk in b.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;

        // Global soft-delete filter
        b.Entity<Campus>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<School>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<Programme>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<Course>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<Student>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<Enrollment>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<StaffFaculty>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<Exam>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<Question>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<ExamQuestion>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<ExamResult>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<Room>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<ExamRoomAllocation>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<ExamInvigilator>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<SpecialArrangement>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<Document>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<DocumentVersion>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<FeeType>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<FinancialTransaction>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<GradeScheme>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<GradeBand>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<GradeChangeRequest>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<Lead>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<AdmissionApplication>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<ApplicationDocument>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<AdmissionRecommendation>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<CaseType>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<Case>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<CaseAction>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<Notification>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<KnowledgeArticle>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<AssistantQueryLog>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<Role>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<Permission>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<RolePermission>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<UserRoleAssignment>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<ExternalExaminer>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<ExaminerAssignment>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<ExaminerReport>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<ExaminerPortalToken>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<QuestionPaper>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<QuestionPaperVersion>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<PrintRun>().HasQueryFilter(x => !x.IsDeleted);
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.ModifiedUtc = DateTimeOffset.UtcNow;
            if (entry.State == EntityState.Deleted)   // convert hard delete -> soft delete
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
            }
        }
        return base.SaveChangesAsync(ct);
    }
}
