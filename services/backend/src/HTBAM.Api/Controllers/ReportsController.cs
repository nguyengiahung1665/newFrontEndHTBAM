using ClosedXML.Excel;
using HTBAM.Api.Support;
using HTBAM.Application.Interfaces;
using HTBAM.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace HTBAM.Api.Controllers;

[ApiController, Authorize, Route("api/reports")]
public sealed class ReportsController(AppDbContext db, IAutoCommentService autoComments) : ControllerBase
{
    [HttpGet("sessions/{id:long}")]
    public async Task<ActionResult> Session(long id, CancellationToken ct)
    {
        if (!await AccessScope.CanExportSessionReportAsync(db, User, id, ct)) return Forbid();
        return Ok(await BuildSession(id, ct));
    }

    [HttpGet("students/{id:long}")]
    public async Task<ActionResult> Student(long id, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        if (!await AccessScope.CanViewStudentAsync(db, User, id, ct)) return Forbid();
        var student = await db.StudentsSet.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (student is null) return NotFound();
        var permitted = ScopedSessions(from, to, ct).Select(x => x.Id);
        var summaries = await db.StudentSessionSummariesSet.AsNoTracking()
            .Where(x => x.StudentId == id && permitted.Contains(x.SessionId))
            .Join(db.SessionsSet, x => x.SessionId, s => s.Id, (x, s) => new { x.SessionId, s.ScheduledStart, s.ClassSectionId, x.FocusedRatio, x.DistractedRatio, x.SleepyRatio, x.ActiveRatio, x.AlertCount, x.AlertDurationSeconds, x.ObservationQuality, x.AutoComment, x.AutoCommentProvider, x.AutoCommentGeneratedAt })
            .OrderByDescending(x => x.ScheduledStart)
            .ToListAsync(ct);
        var attendance = await db.AttendanceSet.AsNoTracking().Where(x => x.StudentId == id && permitted.Contains(x.SessionId)).ToListAsync(ct);
        return Ok(new { student = new { student.Id, student.StudentCode, student.FullName, student.Email }, summary = new { SessionCount = summaries.Count, AverageFocused = summaries.Count == 0 ? 0 : summaries.Average(x => x.FocusedRatio), AverageDistracted = summaries.Count == 0 ? 0 : summaries.Average(x => x.DistractedRatio), AverageSleepy = summaries.Count == 0 ? 0 : summaries.Average(x => x.SleepyRatio), AverageActive = summaries.Count == 0 ? 0 : summaries.Average(x => x.ActiveRatio), AverageAttendanceScore = attendance.Count == 0 ? 0 : attendance.Average(x => x.Score), TotalObservedSeconds = attendance.Sum(x => x.ObservedSeconds), TotalAlerts = summaries.Sum(x => x.AlertCount) }, sessions = summaries, attendance });
    }

    [HttpGet("class-sections/{id:long}")]
    public async Task<ActionResult> Class(long id, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        if (!await AccessScope.CanViewClassSectionAsync(db, User, id, ct)) return Forbid();
        var cls = await db.ClassSections.AsNoTracking().Include(x => x.Course).Include(x => x.Teacher).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (cls is null) return NotFound();
        var q = db.SessionsSet.AsNoTracking().Where(x => x.ClassSectionId == id && x.Status == "COMPLETED");
        if (from != null) q = q.Where(x => x.ScheduledStart >= from);
        if (to != null) q = q.Where(x => x.ScheduledStart < to);
        var sessionIds = q.Select(x => x.Id);
        var timeline = await db.ClassSessionSummariesSet.AsNoTracking().Where(x => sessionIds.Contains(x.SessionId))
            .Join(db.SessionsSet, s => s.SessionId, x => x.Id, (s, x) => new { s.SessionId, x.ScheduledStart, s.ObservedStudentCount, s.FocusedRatio, s.DistractedRatio, s.SleepyRatio, s.ActiveRatio, s.AlertCount, s.CameraAiQuality, s.AutoComment, s.AutoCommentProvider, s.AutoCommentGeneratedAt })
            .OrderBy(x => x.ScheduledStart).ToListAsync(ct);
        var students = await db.StudentSessionSummariesSet.AsNoTracking().Where(x => sessionIds.Contains(x.SessionId))
            .GroupBy(x => x.StudentId)
            .Select(g => new { StudentId = g.Key, Focused = g.Average(x => x.FocusedRatio), Distracted = g.Average(x => x.DistractedRatio), Sleepy = g.Average(x => x.SleepyRatio), Active = g.Average(x => x.ActiveRatio), Alerts = g.Sum(x => x.AlertCount), Quality = g.Average(x => x.ObservationQuality) })
            .Join(db.StudentsSet, x => x.StudentId, s => s.Id, (x, s) => new { x.StudentId, s.StudentCode, s.FullName, x.Focused, x.Distracted, x.Sleepy, x.Active, x.Alerts, x.Quality, NeedsAttention = x.Distracted >= .30m || x.Sleepy >= .15m || x.Alerts >= 3 })
            .OrderByDescending(x => x.NeedsAttention).ThenByDescending(x => x.Distracted).ToListAsync(ct);
        return Ok(new { classSection = new { cls.Id, cls.Code, cls.Name, Course = cls.Course.Name, Teacher = cls.Teacher.FullName, cls.Semester, cls.AcademicYear, cls.AutoComment, cls.AutoCommentProvider, cls.AutoCommentGeneratedAt }, summary = new { SessionCount = timeline.Count, Focused = timeline.Count == 0 ? 0 : timeline.Average(x => x.FocusedRatio), Distracted = timeline.Count == 0 ? 0 : timeline.Average(x => x.DistractedRatio), Sleepy = timeline.Count == 0 ? 0 : timeline.Average(x => x.SleepyRatio), Active = timeline.Count == 0 ? 0 : timeline.Average(x => x.ActiveRatio), Alerts = timeline.Sum(x => x.AlertCount) }, timeline, students, attention = students.Where(x => x.NeedsAttention).ToList() });
    }

    [HttpPost("sessions/{id:long}/auto-comment"), Authorize(Roles = "LECTURER")]
    public async Task<ActionResult> GenerateSessionComment(long id, CancellationToken ct)
    {
        if (!await AccessScope.CanViewSessionAsync(db, User, id, ct)) return Forbid();
        var result = await autoComments.GenerateSessionAsync(id, UserContext.Id(User), ct);
        return result is null ? Conflict("Buổi học chưa có dữ liệu tổng hợp.") : Ok(result);
    }

    [HttpPost("sessions/{sessionId:long}/students/{studentId:long}/auto-comment"), Authorize(Roles = "LECTURER")]
    public async Task<ActionResult> GenerateStudentComment(long sessionId, long studentId, CancellationToken ct)
    {
        if (!await AccessScope.CanViewSessionAsync(db, User, sessionId, ct)) return Forbid();
        var result = await autoComments.GenerateStudentAsync(sessionId, studentId, UserContext.Id(User), ct);
        return result is null ? Conflict("Chưa có dữ liệu tổng hợp sinh viên.") : Ok(result);
    }

    [HttpPost("class-sections/{id:long}/auto-comment"), Authorize(Roles = "LECTURER")]
    public async Task<ActionResult> GenerateClassSectionComment(long id, CancellationToken ct)
    {
        if (!await AccessScope.CanViewClassSectionAsync(db, User, id, ct)) return Forbid();
        var result = await autoComments.GenerateClassSectionAsync(id, UserContext.Id(User), ct);
        return result is null ? Conflict("Lớp chưa có dữ liệu tổng hợp.") : Ok(result);
    }

    [HttpGet("sessions/{id:long}/excel")]
    public async Task<IActionResult> SessionExcel(long id, CancellationToken ct)
    {
        if (!await AccessScope.CanExportSessionReportAsync(db, User, id, ct)) return Forbid();
        var report = await BuildSession(id, ct);
        return File(BuildSessionExcel(report), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"HTBAM-Session-{id}.xlsx");
    }

    [HttpGet("sessions/{id:long}/pdf")]
    public async Task<IActionResult> SessionPdf(long id, CancellationToken ct)
    {
        if (!await AccessScope.CanExportSessionReportAsync(db, User, id, ct)) return Forbid();
        var report = await BuildSession(id, ct);
        return File(BuildSessionPdf(report), "application/pdf", $"HTBAM-Session-{id}.pdf");
    }

    [HttpGet("students/{id:long}/excel")]
    public async Task<IActionResult> StudentExcel(long id, CancellationToken ct) => await StudentFile(id, "excel", ct);

    [HttpGet("students/{id:long}/pdf")]
    public async Task<IActionResult> StudentPdf(long id, CancellationToken ct) => await StudentFile(id, "pdf", ct);

    [HttpGet("class-sections/{id:long}/excel")]
    public async Task<IActionResult> ClassExcel(long id, CancellationToken ct)
    {
        if (!await AccessScope.CanViewClassSectionAsync(db, User, id, ct)) return Forbid();
        var sessions = await db.SessionsSet.AsNoTracking().Where(x => x.ClassSectionId == id).OrderBy(x => x.ScheduledStart).ToListAsync(ct);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sessions");
        string[] h = ["SessionId", "ScheduledStart", "Status", "Focused", "Distracted", "Sleepy", "Active"];
        for (var i = 0; i < h.Length; i++) ws.Cell(1, i + 1).Value = h[i];
        var row = 2;
        foreach (var s in sessions)
        {
            var sum = await db.ClassSessionSummariesSet.AsNoTracking().FirstOrDefaultAsync(x => x.SessionId == s.Id, ct);
            ws.Cell(row, 1).Value = s.Id; ws.Cell(row, 2).Value = s.ScheduledStart; ws.Cell(row, 3).Value = s.Status; ws.Cell(row, 4).Value = sum?.FocusedRatio ?? 0; ws.Cell(row, 5).Value = sum?.DistractedRatio ?? 0; ws.Cell(row, 6).Value = sum?.SleepyRatio ?? 0; ws.Cell(row, 7).Value = sum?.ActiveRatio ?? 0; row++;
        }
        using var ms = new MemoryStream(); wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"HTBAM-Class-{id}.xlsx");
    }

    [HttpGet("class-sections/{id:long}/pdf")]
    public async Task<IActionResult> ClassPdf(long id, CancellationToken ct)
    {
        if (!await AccessScope.CanViewClassSectionAsync(db, User, id, ct)) return Forbid();
        var cls = await db.ClassSections.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (cls is null) return NotFound();
        var pdf = Document.Create(c => c.Page(p => { p.Size(PageSizes.A4); p.Margin(32); p.DefaultTextStyle(x => x.FontSize(10)); p.Header().Text($"HTBAM - Class {cls.Code}").Bold().FontSize(18); p.Content().Text(cls.Name); })).GeneratePdf();
        return File(pdf, "application/pdf", $"HTBAM-Class-{id}.pdf");
    }

    private IQueryable<HTBAM.Domain.Entities.Session> ScopedSessions(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var tid = AccessScope.TeacherIdAsync(db, User, ct).GetAwaiter().GetResult();
        var sq = AccessScope.Sessions(db, User, tid).AsNoTracking();
        if (from != null) sq = sq.Where(x => x.ScheduledStart >= from);
        if (to != null) sq = sq.Where(x => x.ScheduledStart < to);
        return sq;
    }

    private async Task<IActionResult> StudentFile(long id, string format, CancellationToken ct)
    {
        if (!await AccessScope.CanViewStudentAsync(db, User, id, ct)) return Forbid();
        var student = await db.StudentsSet.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (student is null) return NotFound();
        var permitted = ScopedSessions(null, null, ct).Select(x => x.Id);
        var rows = await db.StudentSessionSummariesSet.AsNoTracking().Where(x => x.StudentId == id && permitted.Contains(x.SessionId)).Join(db.SessionsSet, x => x.SessionId, s => s.Id, (x, s) => new { x.SessionId, s.ScheduledStart, x.FocusedRatio, x.DistractedRatio, x.SleepyRatio, x.ActiveRatio, x.AlertCount, x.ObservationQuality }).OrderBy(x => x.ScheduledStart).ToListAsync(ct);
        if (format == "pdf")
        {
            var pdf = Document.Create(c => c.Page(p => { p.Size(PageSizes.A4); p.Margin(32); p.DefaultTextStyle(x => x.FontSize(10)); p.Header().Text($"HTBAM - Student {student.StudentCode}").Bold().FontSize(18); p.Content().Text($"{student.FullName} - sessions: {rows.Count}"); })).GeneratePdf();
            return File(pdf, "application/pdf", $"HTBAM-Student-{student.StudentCode}.pdf");
        }
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Student");
        string[] h = ["SessionId", "ScheduledStart", "Focused", "Distracted", "Sleepy", "Active", "Alerts", "Quality"];
        for (var i = 0; i < h.Length; i++) ws.Cell(1, i + 1).Value = h[i];
        var row = 2;
        foreach (var x in rows) { ws.Cell(row, 1).Value = x.SessionId; ws.Cell(row, 2).Value = x.ScheduledStart; ws.Cell(row, 3).Value = x.FocusedRatio; ws.Cell(row, 4).Value = x.DistractedRatio; ws.Cell(row, 5).Value = x.SleepyRatio; ws.Cell(row, 6).Value = x.ActiveRatio; ws.Cell(row, 7).Value = x.AlertCount; ws.Cell(row, 8).Value = x.ObservationQuality; row++; }
        using var ms = new MemoryStream(); wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"HTBAM-Student-{student.StudentCode}.xlsx");
    }

    private async Task<object> BuildSession(long id, CancellationToken ct)
    {
        var session = await db.SessionsSet.AsNoTracking().Include(x => x.ClassSection).ThenInclude(x => x.Course).Include(x => x.ClassSection).ThenInclude(x => x.Teacher).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Session không tồn tại.");
        var substitution = await db.SessionSubstitutionsSet.AsNoTracking().Where(x => x.SessionId == id && (x.Status == "ACTIVE" || x.Status == "COMPLETED")).OrderByDescending(x => x.AssignedAt).Select(x => new { x.SubstituteTeacherId, SubstituteTeacher = x.SubstituteTeacher.FullName, x.Reason, x.Status, x.CompletedAt }).FirstOrDefaultAsync(ct);
        var cls = await db.ClassSessionSummariesSet.AsNoTracking().FirstOrDefaultAsync(x => x.SessionId == id, ct);
        var students = await db.StudentSessionSummariesSet.AsNoTracking().Where(x => x.SessionId == id).Join(db.StudentsSet, x => x.StudentId, s => s.Id, (x, s) => new { x.StudentId, s.StudentCode, s.FullName, x.FocusedRatio, x.DistractedRatio, x.SleepyRatio, x.ActiveRatio, x.AlertCount, x.AlertDurationSeconds, x.ObservationQuality, x.AutoComment, x.AutoCommentProvider, x.AutoCommentGeneratedAt }).OrderBy(x => x.StudentCode).ToListAsync(ct);
        var attendance = await db.AttendanceSet.AsNoTracking().Where(x => x.SessionId == id).ToListAsync(ct);
        var timeline = await db.BehaviorEventsSet.AsNoTracking().Where(x => x.SessionId == id).OrderBy(x => x.StartedAt).Take(10000).ToListAsync(ct);
        var alerts = await db.AlertsSet.AsNoTracking().Where(x => x.SessionId == id).OrderBy(x => x.StartedAt).ToListAsync(ct);
        return new { session = new { session.Id, session.ClassSectionId, ClassSection = session.ClassSection.Code, Course = session.ClassSection.Course.Name, Teacher = session.ClassSection.Teacher.FullName, session.OriginalTeacherId, ActiveSubstitution = substitution, session.ScheduledStart, session.StartedAt, session.EndedAt, session.Status, session.LecturerComment }, summary = cls, students, attendance, timeline, alerts, attention = students.Where(x => x.DistractedRatio >= .30m || x.SleepyRatio >= .15m || x.AlertCount >= 3).ToList() };
    }

    private static byte[] BuildSessionExcel(object report)
    {
        dynamic r = report;
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Overview");
        ws.Cell("A1").Value = "HTBAM SESSION REPORT"; ws.Cell("A3").Value = "Session"; ws.Cell("B3").Value = (long)r.session.Id; ws.Cell("A4").Value = "Class"; ws.Cell("B4").Value = (string)r.session.ClassSection; ws.Cell("A5").Value = "Course"; ws.Cell("B5").Value = (string)r.session.Course;
        var students = wb.Worksheets.Add("Students");
        string[] heads = ["StudentCode", "FullName", "Focused", "Distracted", "Sleepy", "Active", "Alerts", "Quality"];
        for (var i = 0; i < heads.Length; i++) students.Cell(1, i + 1).Value = heads[i];
        var row = 2;
        foreach (var s in r.students) { students.Cell(row, 1).Value = (string)s.StudentCode; students.Cell(row, 2).Value = (string)s.FullName; students.Cell(row, 3).Value = (decimal)s.FocusedRatio; students.Cell(row, 4).Value = (decimal)s.DistractedRatio; students.Cell(row, 5).Value = (decimal)s.SleepyRatio; students.Cell(row, 6).Value = (decimal)s.ActiveRatio; students.Cell(row, 7).Value = (int)s.AlertCount; students.Cell(row, 8).Value = (decimal)s.ObservationQuality; row++; }
        using var ms = new MemoryStream(); wb.SaveAs(ms); return ms.ToArray();
    }

    private static byte[] BuildSessionPdf(object report)
    {
        dynamic r = report;
        return Document.Create(c => c.Page(p => { p.Size(PageSizes.A4); p.Margin(32); p.DefaultTextStyle(x => x.FontSize(10)); p.Header().Text($"HTBAM - Session #{r.session.Id}").Bold().FontSize(18); p.Content().Column(col => { col.Spacing(6); col.Item().Text($"Class: {r.session.ClassSection}"); col.Item().Text($"Course: {r.session.Course}"); col.Item().Text($"Teacher: {r.session.Teacher}"); }); })).GeneratePdf();
    }
}
