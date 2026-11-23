using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Exams;

public record PostResultsOutcome(Guid BatchId, int Posted, int Skipped);

/// <summary>Examinations logic: assemble exams from the question bank, keep TotalMarks in step,
/// and post results into the gradebook as one undoable batch (reusing Enrollment.ImportBatchId,
/// so exam grades flow to the Grades module and on to Moodle — and can be rolled back).</summary>
public class ExamService
{
    private readonly SmsDbContext _db;
    public ExamService(SmsDbContext db) => _db = db;

    public async Task<ExamQuestion> AddQuestionAsync(int examId, int questionId, double? marks, CancellationToken ct = default)
    {
        var exam = await _db.Exams.Include(e => e.Questions).FirstOrDefaultAsync(e => e.Id == examId, ct)
                   ?? throw new InvalidOperationException($"Exam {examId} not found.");
        var question = await _db.Questions.FirstOrDefaultAsync(q => q.Id == questionId, ct)
                       ?? throw new InvalidOperationException($"Question {questionId} not found.");
        if (exam.Questions.Any(q => q.QuestionId == questionId))
            throw new InvalidOperationException("Question is already on this exam.");

        var eq = new ExamQuestion
        {
            ExamId = examId, QuestionId = questionId,
            Order = exam.Questions.Count + 1,
            Marks = marks ?? question.DefaultMarks
        };
        _db.ExamQuestions.Add(eq);
        // EF fixup may already have added eq to exam.Questions; ensure it's present exactly once,
        // then derive the total from the collection (avoids double-counting the new row).
        if (!exam.Questions.Contains(eq)) exam.Questions.Add(eq);
        exam.TotalMarks = exam.Questions.Sum(q => q.Marks);
        await _db.SaveChangesAsync(ct);
        return eq;
    }

    public async Task RecordResultAsync(int examId, int studentId, double score, CancellationToken ct = default)
    {
        var result = await _db.ExamResults.FirstOrDefaultAsync(r => r.ExamId == examId && r.StudentId == studentId, ct);
        if (result is null)
        {
            result = new ExamResult { ExamId = examId, StudentId = studentId };
            _db.ExamResults.Add(result);
        }
        result.Score = score;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Post every recorded result for an exam into the matching Enrollment's
    /// FinalExamMark as one undoable batch.</summary>
    public async Task<PostResultsOutcome> PostResultsToGradebookAsync(int examId, CancellationToken ct = default)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == examId, ct)
                   ?? throw new InvalidOperationException($"Exam {examId} not found.");
        var results = await _db.ExamResults.Where(r => r.ExamId == examId).ToListAsync(ct);
        var enrollments = await _db.Enrollments
            .Where(e => e.CourseId == exam.CourseId && e.TermId == exam.TermId).ToListAsync(ct);
        var byStudent = enrollments.ToDictionary(e => e.StudentId);

        var batchId = Guid.NewGuid();
        int posted = 0, skipped = 0;
        foreach (var r in results)
        {
            if (!byStudent.TryGetValue(r.StudentId, out var e)) { skipped++; continue; }
            e.FinalExamMark = r.Score;
            e.GradeSource = "Exam";
            e.GradedUtc = DateTimeOffset.UtcNow;
            e.ImportBatchId = batchId;
            r.Posted = true;
            r.PostBatchId = batchId;
            posted++;
        }
        exam.Status = ExamStatus.Completed;
        await _db.SaveChangesAsync(ct);
        return new PostResultsOutcome(batchId, posted, skipped);
    }
}
