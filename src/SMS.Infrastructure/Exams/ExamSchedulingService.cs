using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Exams;

public record Clash(ClashType Type, ClashSeverity Severity, string Description, int ExamId, int? OtherExamId, int? StudentId);

/// <summary>Exam timetabling: allocate rooms (capacity-checked), assign invigilators, manage special
/// arrangements, and detect clashes (student overlaps, double-booked rooms/invigilators, capacity,
/// too many exams a day) — the EMS brief's automatic clash checks.</summary>
public class ExamSchedulingService
{
    private readonly SmsDbContext _db;
    public ExamSchedulingService(SmsDbContext db) => _db = db;

    public int MaxExamsPerDay { get; set; } = 2;

    public async Task<ExamRoomAllocation> AllocateRoomAsync(int examId, int roomId, int seats, CancellationToken ct = default)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == roomId, ct)
                   ?? throw new InvalidOperationException($"Room {roomId} not found.");
        if (seats > room.Capacity)
            throw new InvalidOperationException($"Cannot allocate {seats} seats; room '{room.Name}' holds {room.Capacity}.");
        var alloc = new ExamRoomAllocation { ExamId = examId, RoomId = roomId, SeatsAllocated = seats };
        _db.ExamRoomAllocations.Add(alloc);
        await _db.SaveChangesAsync(ct);
        return alloc;
    }

    public async Task AssignInvigilatorAsync(int examId, int staffId, InvigilatorRole role, CancellationToken ct = default)
    {
        _db.ExamInvigilators.Add(new ExamInvigilator { ExamId = examId, StaffFacultyId = staffId, Role = role });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<SpecialArrangement> AddArrangementAsync(SpecialArrangement a, CancellationToken ct = default)
    {
        _db.SpecialArrangements.Add(a);
        await _db.SaveChangesAsync(ct);
        return a;
    }

    /// <summary>Detect all clashes among the scheduled exams in a term.</summary>
    public async Task<IReadOnlyList<Clash>> DetectClashesAsync(int termId, CancellationToken ct = default)
    {
        var exams = await _db.Exams.AsNoTracking()
            .Where(e => e.TermId == termId && e.ScheduledStart != null)
            .Select(e => new { e.Id, e.Title, e.CourseId, Start = e.ScheduledStart!.Value, e.DurationMinutes })
            .ToListAsync(ct);

        var allocByExam = (await _db.ExamRoomAllocations.AsNoTracking()
            .Where(a => exams.Select(e => e.Id).Contains(a.ExamId)).ToListAsync(ct))
            .GroupBy(a => a.ExamId).ToDictionary(g => g.Key, g => g.ToList());
        var invByExam = (await _db.ExamInvigilators.AsNoTracking()
            .Where(i => exams.Select(e => e.Id).Contains(i.ExamId)).ToListAsync(ct))
            .GroupBy(i => i.ExamId).ToDictionary(g => g.Key, g => g.Select(x => x.StaffFacultyId).ToHashSet());

        // students per exam (registered enrolments for the exam's course+term)
        var studentsByExam = new Dictionary<int, HashSet<int>>();
        foreach (var e in exams)
            studentsByExam[e.Id] = (await _db.Enrollments.AsNoTracking()
                .Where(en => en.CourseId == e.CourseId && en.TermId == termId && en.Status == EnrollmentStatus.Registered)
                .Select(en => en.StudentId).ToListAsync(ct)).ToHashSet();

        var extraTime = (await _db.SpecialArrangements.AsNoTracking()
            .Where(a => a.Approved && a.Type == SpecialArrangementType.ExtraTime).ToListAsync(ct))
            .GroupBy(a => a.StudentId).ToDictionary(g => g.Key, g => g.Max(x => x.ExtraTimePercent));

        var clashes = new List<Clash>();

        // capacity per exam
        foreach (var e in exams)
        {
            int enrolled = studentsByExam[e.Id].Count;
            int seats = allocByExam.TryGetValue(e.Id, out var al) ? al.Sum(x => x.SeatsAllocated) : 0;
            if (seats > 0 && enrolled > seats)
                clashes.Add(new Clash(ClashType.CapacityExceeded, ClashSeverity.Error,
                    $"'{e.Title}': {enrolled} candidates but only {seats} seats allocated.", e.Id, null, null));
        }

        double maxExtraPct = extraTime.Count == 0 ? 0 : extraTime.Values.Max() / 100.0;

        // pairwise overlap checks
        for (int i = 0; i < exams.Count; i++)
        for (int j = i + 1; j < exams.Count; j++)
        {
            var a = exams[i]; var b = exams[j];
            var aEnd = a.Start.AddMinutes(a.DurationMinutes);
            var bEnd = b.Start.AddMinutes(b.DurationMinutes);
            bool strictOverlap = a.Start < bEnd && b.Start < aEnd;
            // Widen the gate so a back-to-back pair is still checked for students who have extra time.
            var aEndMax = a.Start.AddMinutes(a.DurationMinutes * (1 + maxExtraPct));
            var bEndMax = b.Start.AddMinutes(b.DurationMinutes * (1 + maxExtraPct));
            if (!(a.Start < bEndMax && b.Start < aEndMax)) continue;

            // room double-booking (rooms/invigilators use the strict time overlap, not student extra time)
            if (strictOverlap && allocByExam.TryGetValue(a.Id, out var ra) && allocByExam.TryGetValue(b.Id, out var rb))
                foreach (var roomId in ra.Select(x => x.RoomId).Intersect(rb.Select(x => x.RoomId)))
                    clashes.Add(new Clash(ClashType.RoomDoubleBooked, ClashSeverity.Error,
                        $"Room {roomId} double-booked by '{a.Title}' and '{b.Title}'.", a.Id, b.Id, null));

            // invigilator double-booking
            if (strictOverlap && invByExam.TryGetValue(a.Id, out var ia) && invByExam.TryGetValue(b.Id, out var ib))
                foreach (var staffId in ia.Intersect(ib))
                    clashes.Add(new Clash(ClashType.InvigilatorDoubleBooked, ClashSeverity.Error,
                        $"Invigilator {staffId} double-booked by '{a.Title}' and '{b.Title}'.", a.Id, b.Id, staffId));

            // student overlap (accounting for extra time on a's/b's end)
            foreach (var studentId in studentsByExam[a.Id].Intersect(studentsByExam[b.Id]))
            {
                var aEndS = a.Start.AddMinutes(a.DurationMinutes * (1 + Pct(extraTime, studentId)));
                var bEndS = b.Start.AddMinutes(b.DurationMinutes * (1 + Pct(extraTime, studentId)));
                if (a.Start < bEndS && b.Start < aEndS)
                    clashes.Add(new Clash(ClashType.StudentOverlap, ClashSeverity.Error,
                        $"Student {studentId} has overlapping exams '{a.Title}' and '{b.Title}'.", a.Id, b.Id, studentId));
            }
        }

        // too many exams per day per student
        foreach (var studentExams in exams
            .SelectMany(e => studentsByExam[e.Id].Select(s => (Student: s, Day: DateOnly.FromDateTime(e.Start.Date), e.Id, e.Title)))
            .GroupBy(x => new { x.Student, x.Day }))
        {
            var list = studentExams.ToList();
            if (list.Count > MaxExamsPerDay)
                clashes.Add(new Clash(ClashType.TooManyExamsPerDay, ClashSeverity.Warning,
                    $"Student {studentExams.Key.Student} has {list.Count} exams on {studentExams.Key.Day} (limit {MaxExamsPerDay}).",
                    list[0].Id, null, studentExams.Key.Student));
        }

        return clashes;
    }

    static double Pct(Dictionary<int, int> extra, int studentId) => extra.TryGetValue(studentId, out var p) ? p / 100.0 : 0;
}
