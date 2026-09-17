using HTBAM.Application.DTOs;
using HTBAM.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HTBAM.Application.Services;

public sealed class AutoCommentService(
    IAppDbContext db,
    ILlmProviderResolver providerResolver,
    IAuditService audit) : IAutoCommentService
{
    public async Task<LlmGenerationResult?> GenerateSessionAsync(long sessionId, long? userId, CancellationToken ct = default)
    {
        var summary = await db.ClassSessionSummaries.FirstOrDefaultAsync(x => x.SessionId == sessionId, ct);
        if (summary is null) return null;

        var subject = await db.Sessions.Where(x => x.Id == sessionId)
            .Select(x => new { x.ClassSection.Code, x.ClassSection.Name })
            .FirstAsync(ct);
        var attendance = await db.Attendance.Where(x => x.SessionId == sessionId)
            .GroupBy(_ => 1)
            .Select(g => new { Score = (decimal?)g.Average(x => x.Score), Present = (int?)g.Sum(x => x.PresentSeconds), Observed = (int?)g.Sum(x => x.ObservedSeconds) })
            .FirstOrDefaultAsync(ct);
        var hadComment = !string.IsNullOrWhiteSpace(summary.AutoComment);
        var result = await GenerateWithFallbackAsync(new LlmCommentRequest(
            "SESSION", sessionId.ToString(), subject.Code,
            summary.FocusedRatio, summary.DistractedRatio, summary.SleepyRatio, summary.ActiveRatio,
            summary.AlertCount, summary.CameraAiQuality, attendance?.Score, attendance?.Present, attendance?.Observed,
            $"ClassSectionName={subject.Name}; ObservedStudentCount={summary.ObservedStudentCount}"), ct);

        summary.AutoComment = result.Text;
        summary.AutoCommentProvider = ProviderStorageValue(result);
        summary.AutoCommentGeneratedAt = result.GeneratedAt;
        db.Update(summary);
        await db.SaveChangesAsync(ct);
        await WriteAuditAsync(userId, hadComment, "ClassSessionSummary", summary.Id, result, "SESSION", sessionId, ct);
        return result;
    }

    public async Task<LlmGenerationResult?> GenerateStudentAsync(long sessionId, long studentId, long? userId, CancellationToken ct = default)
    {
        var summary = await db.StudentSessionSummaries.FirstOrDefaultAsync(x => x.SessionId == sessionId && x.StudentId == studentId, ct);
        if (summary is null) return null;

        var studentCode = await db.Students.Where(x => x.Id == studentId).Select(x => x.StudentCode).FirstAsync(ct);
        var attendance = await db.Attendance.FirstOrDefaultAsync(x => x.SessionId == sessionId && x.StudentId == studentId, ct);
        var hadComment = !string.IsNullOrWhiteSpace(summary.AutoComment);
        var result = await GenerateWithFallbackAsync(new LlmCommentRequest(
            "STUDENT", studentId.ToString(), studentCode,
            summary.FocusedRatio, summary.DistractedRatio, summary.SleepyRatio, summary.ActiveRatio,
            summary.AlertCount, summary.ObservationQuality, attendance?.Score, attendance?.PresentSeconds, attendance?.ObservedSeconds,
            $"SessionId={sessionId}"), ct);

        summary.AutoComment = result.Text;
        summary.AutoCommentProvider = ProviderStorageValue(result);
        summary.AutoCommentGeneratedAt = result.GeneratedAt;
        db.Update(summary);
        await db.SaveChangesAsync(ct);
        await WriteAuditAsync(userId, hadComment, "StudentSessionSummary", summary.Id, result, "STUDENT", studentId, ct);
        return result;
    }

    public async Task<LlmGenerationResult?> GenerateClassSectionAsync(long classSectionId, long? userId, CancellationToken ct = default)
    {
        var classSection = await db.ClassSections.FirstOrDefaultAsync(x => x.Id == classSectionId, ct);
        if (classSection is null) return null;

        var sessionIds = db.Sessions.Where(x => x.ClassSectionId == classSectionId && x.Status == "COMPLETED").Select(x => x.Id);
        var aggregate = await db.ClassSessionSummaries.Where(x => sessionIds.Contains(x.SessionId))
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Sessions = g.Count(),
                Focused = g.Average(x => x.FocusedRatio),
                Distracted = g.Average(x => x.DistractedRatio),
                Sleepy = g.Average(x => x.SleepyRatio),
                Active = g.Average(x => x.ActiveRatio),
                Alerts = g.Sum(x => x.AlertCount),
                Quality = g.Average(x => x.CameraAiQuality)
            }).FirstOrDefaultAsync(ct);
        if (aggregate is null) return null;

        var attendance = await db.Attendance.Where(x => sessionIds.Contains(x.SessionId))
            .GroupBy(_ => 1)
            .Select(g => new { Score = (decimal?)g.Average(x => x.Score), Present = (int?)g.Sum(x => x.PresentSeconds), Observed = (int?)g.Sum(x => x.ObservedSeconds) })
            .FirstOrDefaultAsync(ct);
        var hadComment = !string.IsNullOrWhiteSpace(classSection.AutoComment);
        var result = await GenerateWithFallbackAsync(new LlmCommentRequest(
            "CLASS_SECTION", classSectionId.ToString(), classSection.Code,
            aggregate.Focused, aggregate.Distracted, aggregate.Sleepy, aggregate.Active,
            aggregate.Alerts, aggregate.Quality, attendance?.Score, attendance?.Present, attendance?.Observed,
            $"SessionCount={aggregate.Sessions}"), ct);

        classSection.AutoComment = result.Text;
        classSection.AutoCommentProvider = ProviderStorageValue(result);
        classSection.AutoCommentGeneratedAt = result.GeneratedAt;
        db.Update(classSection);
        await db.SaveChangesAsync(ct);
        await WriteAuditAsync(userId, hadComment, "ClassSection", classSection.Id, result, "CLASS_SECTION", classSectionId, ct);
        return result;
    }

    private async Task<LlmGenerationResult> GenerateWithFallbackAsync(LlmCommentRequest request, CancellationToken ct)
    {
        var provider = providerResolver.Resolve();
        if (provider.IsConfigured)
        {
            try
            {
                var generated = await provider.GenerateCommentAsync(request, ct);
                if (generated is not null && string.IsNullOrWhiteSpace(generated.ErrorCode) && !string.IsNullOrWhiteSpace(generated.Text))
                    return generated;
                return RuleBased(request, generated?.ErrorCode ?? "PROVIDER_ERROR");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return RuleBased(request, "PROVIDER_EXCEPTION");
            }
        }

        return RuleBased(request, "PROVIDER_NOT_CONFIGURED");
    }

    private static LlmGenerationResult RuleBased(LlmCommentRequest request, string errorCode)
    {
        var subject = request.SubjectType switch
        {
            "STUDENT" => "Sinh viên",
            "CLASS_SECTION" => "Lớp học",
            _ => "Buổi học"
        };
        var core = request.FocusedRatio >= .70m
            ? $"{subject} duy trì tỷ lệ tập trung ở mức tốt trong dữ liệu quan sát."
            : request.DistractedRatio >= .30m
                ? $"{subject} có tỷ lệ mất tập trung đáng chú ý trong dữ liệu quan sát."
                : request.SleepyRatio >= .15m
                    ? $"{subject} có tỷ lệ trạng thái buồn ngủ cần được tiếp tục theo dõi."
                    : $"{subject} có phân bố hành vi tương đối cân bằng trong dữ liệu quan sát.";
        var metrics = $"Tỷ lệ tập trung {request.FocusedRatio:P1}, mất tập trung {request.DistractedRatio:P1}, buồn ngủ {request.SleepyRatio:P1}, hoạt động {request.ActiveRatio:P1}; hệ thống ghi nhận {request.AlertCount} cảnh báo.";
        var quality = request.ObservationQuality < .50m
            ? "Chất lượng quan sát thấp nên nhận xét này có độ tin cậy hạn chế."
            : "Có thể tiếp tục theo dõi xu hướng này ở các buổi học tiếp theo.";
        return new LlmGenerationResult($"{core} {metrics} {quality}", "RULE_BASED_FALLBACK", "RULE_BASED", DateTime.UtcNow, true, errorCode);
    }

    private async Task WriteAuditAsync(long? userId, bool regenerated, string entityType, long entityId, LlmGenerationResult result, string subjectType, long subjectId, CancellationToken ct)
    {
        await audit.WriteAsync(userId, regenerated ? "AUTO_COMMENT_REGENERATE" : "AUTO_COMMENT_GENERATE", entityType, entityId.ToString(), new
        {
            provider = result.Provider,
            model = result.Model,
            subjectType,
            subjectId,
            fallback = result.IsFallback,
            timestamp = result.GeneratedAt,
            errorCode = result.ErrorCode
        }, ct);
        if (result.IsFallback)
            await audit.WriteAsync(userId, "AUTO_COMMENT_FALLBACK", entityType, entityId.ToString(), new { provider = result.Provider, model = result.Model, subjectType, subjectId, fallback = true, timestamp = result.GeneratedAt, errorCode = result.ErrorCode }, ct);
    }

    private static string ProviderStorageValue(LlmGenerationResult result) =>
        result.IsFallback ? "RULE_BASED_FALLBACK" : $"{result.Provider}:{result.Model}";
}
