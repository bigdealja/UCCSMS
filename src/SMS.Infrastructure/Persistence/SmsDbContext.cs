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
        b.Entity<ExamQuestion>().HasIndex(x => new { x.ExamId, x.QuestionId }).IsUnique();

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
