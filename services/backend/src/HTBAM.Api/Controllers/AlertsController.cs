using HTBAM.Api.Support;
using HTBAM.Application.Interfaces;
using HTBAM.Domain.Entities;
using HTBAM.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HTBAM.Api.Controllers;

[ApiController, Authorize(Roles = "ADMIN,LECTURER,TECH_AI"), Route("api/alerts")]
public sealed class AlertsController(AppDbContext db, IAuditService audit) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> List(
        [FromQuery] long? sessionId, [FromQuery] long? studentId, [FromQuery] string? status,
        [FromQuery] string? type, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var query = db.AlertsSet.AsNoTracking().AsQueryable();
        if (sessionId is not null)
        {
            if (!await AccessScope.CanAccessSessionAsync(db, User, sessionId.Value, ct)) return Forbid();
            query = query.Where(x => x.SessionId == sessionId);
        }
        else if (User.IsInRole("LECTURER"))
        {
            var teacherId = await AccessScope.TeacherIdAsync(db, User, ct);
            var allowedSessionIds = db.SessionsSet.Where(s => s.ClassSection.TeacherId == teacherId).Select(s => s.Id);
            query = query.Where(x => allowedSessionIds.Contains(x.SessionId));
        }

        if (studentId is not null) query = query.Where(x => x.StudentId == studentId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(x => x.Type == type.Trim().ToUpperInvariant());
        if (from is not null) query = query.Where(x => x.StartedAt >= from);
        if (to is not null) query = query.Where(x => x.StartedAt < to);
        return Ok(await query.OrderByDescending(x => x.CreatedAt).Take(2000).ToListAsync(ct));
    }

    public record NoteReq(string? Note);

    [HttpPost("{id:long}/ack"), Authorize(Roles = "ADMIN,LECTURER")]
    public Task<ActionResult> Ack(long id, NoteReq request, CancellationToken ct) => Transition(id, "ACK", request.Note, ct);

    [HttpPost("{id:long}/close"), Authorize(Roles = "ADMIN,LECTURER")]
    public Task<ActionResult> Close(long id, NoteReq request, CancellationToken ct) => Transition(id, "CLOSED", request.Note, ct);

    [HttpPost("{id:long}/reopen"), Authorize(Roles = "ADMIN,LECTURER")]
    public Task<ActionResult> Reopen(long id, NoteReq request, CancellationToken ct) => Transition(id, "OPEN", request.Note, ct);

    private async Task<ActionResult> Transition(long id, string target, string? note, CancellationToken ct)
    {
        var alert = await db.AlertsSet.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (alert is null) return NotFound();
        if (!await AccessScope.CanAccessSessionAsync(db, User, alert.SessionId, ct)) return Forbid();
        alert.Status = target;
        alert.LecturerNote = string.IsNullOrWhiteSpace(note) ? alert.LecturerNote : note.Trim();
        if (target == "ACK") alert.AcknowledgedAt = DateTime.UtcNow;
        if (target == "CLOSED") alert.ClosedAt = DateTime.UtcNow;
        if (target == "OPEN") { alert.ClosedAt = null; alert.AcknowledgedAt = null; }
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(UserContext.Id(User), $"ALERT_{target}", "Alert", id.ToString(), new { alert.SessionId, alert.StudentId }, ct);
        return NoContent();
    }

    [HttpGet("rules")]
    public async Task<ActionResult> Rules(CancellationToken ct) =>
        Ok(await db.AlertRulesSet.AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct));

    public record RuleReq(
        string? Code, string? Name, string? BehaviorLabel, int? MinDurationSeconds,
        decimal? MinConfidence, decimal? MinObservationQuality, bool? Enabled, string? Version);

    [HttpPost("rules"), Authorize(Roles = "ADMIN")]
    public async Task<ActionResult> AddRule(RuleReq request, CancellationToken ct)
    {
        var errors = RuleErrors(request);
        if (errors.Count > 0) return ValidationError(errors);
        var code = request.Code!.Trim().ToUpperInvariant();
        if (await db.AlertRulesSet.AnyAsync(x => x.Code == code, ct))
            return Conflict(new { message = $"Mã rule {code} đã tồn tại.", field = "code" });

        var rule = MapRule(new AlertRule(), request);
        db.AlertRulesSet.Add(rule);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(UserContext.Id(User), "ALERT_RULE_CREATE", "AlertRule", rule.Id.ToString(), new { rule.Code }, ct);
        return Ok(rule);
    }

    [HttpPut("rules/{id:long}"), Authorize(Roles = "ADMIN")]
    public async Task<ActionResult> UpdateRule(long id, RuleReq request, CancellationToken ct)
    {
        var rule = await db.AlertRulesSet.FindAsync([id], ct);
        if (rule is null) return NotFound(new { message = "Luật cảnh báo không tồn tại." });
        var errors = RuleErrors(request);
        if (errors.Count > 0) return ValidationError(errors);
        var code = request.Code!.Trim().ToUpperInvariant();
        if (await db.AlertRulesSet.AnyAsync(x => x.Id != id && x.Code == code, ct))
            return Conflict(new { message = $"Mã rule {code} đã tồn tại.", field = "code" });

        MapRule(rule, request);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(UserContext.Id(User), "ALERT_RULE_UPDATE", "AlertRule", id.ToString(), new { rule.Code }, ct);
        return NoContent();
    }

    private BadRequestObjectResult ValidationError(Dictionary<string, string[]> errors) =>
        BadRequest(new { message = "Dữ liệu luật cảnh báo chưa hợp lệ.", errors });

    private static AlertRule MapRule(AlertRule rule, RuleReq request)
    {
        rule.Code = request.Code!.Trim().ToUpperInvariant();
        rule.Name = request.Name!.Trim();
        rule.BehaviorLabel = request.BehaviorLabel!.Trim().ToUpperInvariant();
        rule.MinDurationSeconds = request.MinDurationSeconds!.Value;
        rule.MinConfidence = request.MinConfidence!.Value;
        rule.MinObservationQuality = request.MinObservationQuality!.Value;
        rule.Enabled = request.Enabled!.Value;
        rule.Version = request.Version!.Trim();
        return rule;
    }

    private static Dictionary<string, string[]> RuleErrors(RuleReq request)
    {
        var errors = new Dictionary<string, string[]>();
        var code = request.Code?.Trim() ?? "";
        var name = request.Name?.Trim() ?? "";
        var behavior = request.BehaviorLabel?.Trim().ToUpperInvariant() ?? "";
        var version = request.Version?.Trim() ?? "";
        var labels = new HashSet<string>(StringComparer.Ordinal) { "DISTRACTED", "SLEEPY", "PHONE_USE", "OUT_OF_VIEW" };

        if (code.Length == 0) errors["code"] = ["Mã rule là bắt buộc."];
        else if (code.Length > 64) errors["code"] = ["Mã rule không được vượt quá 64 ký tự."];
        if (name.Length == 0) errors["name"] = ["Tên rule là bắt buộc."];
        else if (name.Length > 256) errors["name"] = ["Tên rule không được vượt quá 256 ký tự."];
        if (behavior.Length == 0) errors["behaviorLabel"] = ["Behavior là bắt buộc."];
        else if (!labels.Contains(behavior)) errors["behaviorLabel"] = ["Behavior không được hỗ trợ."];
        if (request.MinDurationSeconds is null) errors["minDurationSeconds"] = ["Duration là bắt buộc."];
        else if (request.MinDurationSeconds <= 0) errors["minDurationSeconds"] = ["Duration phải lớn hơn 0."];
        if (request.MinConfidence is null) errors["minConfidence"] = ["Confidence là bắt buộc."];
        else if (request.MinConfidence < 0 || request.MinConfidence > 1) errors["minConfidence"] = ["Confidence phải nằm trong khoảng 0 đến 1."];
        if (request.MinObservationQuality is null) errors["minObservationQuality"] = ["Quality là bắt buộc."];
        else if (request.MinObservationQuality < 0 || request.MinObservationQuality > 1) errors["minObservationQuality"] = ["Quality phải nằm trong khoảng 0 đến 1."];
        if (request.Enabled is null) errors["enabled"] = ["Trạng thái Enabled là bắt buộc."];
        if (version.Length == 0) errors["version"] = ["Version là bắt buộc."];
        else if (version.Length > 32) errors["version"] = ["Version không được vượt quá 32 ký tự."];
        return errors;
    }
}
