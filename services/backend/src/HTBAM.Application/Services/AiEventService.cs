using HTBAM.Application.DTOs;
using HTBAM.Application.Interfaces;
using HTBAM.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HTBAM.Application.Services;

public sealed class AiEventService(IAppDbContext db, IAlertEngine alertEngine) : IAiEventService
{
    private static readonly HashSet<string> BehaviorLabels = new(StringComparer.OrdinalIgnoreCase)
    { "FOCUSED", "DISTRACTED", "SLEEPY", "ACTIVE", "OUT_OF_VIEW", "PHONE_USE" };
    private static readonly HashSet<string> ExclusiveStateLabels = new(StringComparer.OrdinalIgnoreCase)
    { "FOCUSED", "DISTRACTED", "SLEEPY", "ACTIVE", "OUT_OF_VIEW" };

    public async Task<bool> ProcessAsync(AiEventEnvelope e, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(e.EventId) || e.EventId.Length > 128) throw new InvalidOperationException("event_id không hợp lệ.");
        if (string.IsNullOrWhiteSpace(e.StableId) || e.StableId.Length > 128) throw new InvalidOperationException("stable_id không hợp lệ.");
        if (e.ObservationQuality is < 0 or > 1) throw new InvalidOperationException("ObservationQuality phải 0..1.");
        var hasBehavior = !string.IsNullOrWhiteSpace(e.BehaviorLabel);
        var hasAlert = !string.IsNullOrWhiteSpace(e.AlertType);
        if (!hasBehavior && !hasAlert && !IsIdentityOrTrackEvent(e.EventType)) return false;
        if (await db.AiEventReceipts.AnyAsync(x => x.ExternalEventId == e.EventId, ct)) return false;

        var session = await db.Sessions.FirstOrDefaultAsync(x => x.Id == e.SessionId, ct) ?? throw new InvalidOperationException("Session không tồn tại.");
        if (session.Status is not ("RUNNING" or "FINALIZING"))
            throw new InvalidOperationException($"Không nhận AI event khi Session đang {session.Status}.");

        var roster = await db.SessionStudents.Where(x => x.SessionId == e.SessionId).Select(x => x.StudentId).ToArrayAsync(ct);
        var candidateInRoster = e.StudentId is not null && roster.Contains(e.StudentId.Value);

        var stable = await db.StableIdentities.FirstOrDefaultAsync(x => x.SessionId == e.SessionId && x.StableId == e.StableId, ct);
        var candidateAlreadyClaimed = candidateInRoster && await db.StableIdentities.AnyAsync(x =>
            x.SessionId == e.SessionId && x.StudentId == e.StudentId && x.State != "CLOSED" && x.StableId != e.StableId, ct);
        var candidateAssignable = candidateInRoster && !candidateAlreadyClaimed;
        var backfillMode = "NONE"; // NONE / UNKNOWN_ONLY / ALL
        if (stable is null)
        {
            stable = new StableIdentity
            {
                SessionId = e.SessionId,
                StableId = e.StableId,
                StudentId = candidateAssignable ? e.StudentId : null,
                IdentityStatus = candidateAlreadyClaimed ? "CONFLICT" : candidateAssignable ? "IDENTIFIED" : "UNKNOWN",
                IdentityConfidence = candidateAssignable ? e.IdentityConfidence : null,
                IdentityMargin = candidateAssignable ? e.IdentityMargin : null,
                FirstSeenAt = e.Timestamp,
                LastSeenAt = e.Timestamp,
                State = IsLostEvent(e.EventType) ? "LOST" : "ACTIVE",
                CurrentTrackId = e.TrackId,
                ModelVersion = e.ModelVersion
            };
            if (candidateAssignable) backfillMode = "UNKNOWN_ONLY";
            await db.AddAsync(stable, ct);
            await db.SaveChangesAsync(ct); // cần Id cho TrackSegment/BehaviorEvent
        }
        else
        {
            stable.LastSeenAt = e.Timestamp > stable.LastSeenAt ? e.Timestamp : stable.LastSeenAt;
            stable.ModelVersion = e.ModelVersion;
            stable.CurrentTrackId = string.IsNullOrWhiteSpace(e.TrackId) ? stable.CurrentTrackId : e.TrackId;
            stable.State = IsLostEvent(e.EventType) ? "LOST" : "ACTIVE";

            if (e.StudentId is not null)
            {
                if (!candidateInRoster)
                {
                    // Roster là candidate constraint. Không gán người ngoài roster vào MSSV của session.
                    if (stable.StudentId is null) stable.IdentityStatus = "UNKNOWN";
                }
                else if (stable.StudentId is null && candidateAlreadyClaimed)
                {
                    // Cùng MSSV đã thuộc một stable_id khác chưa CLOSED: không tạo hai identity cho một sinh viên.
                    // Resolver phải re-link về stable_id cũ hoặc gửi quyết định xử lý conflict rõ ràng.
                    stable.IdentityStatus = "CONFLICT";
                }
                else if (stable.StudentId is null)
                {
                    stable.StudentId = e.StudentId;
                    stable.IdentityStatus = "IDENTIFIED";
                    stable.IdentityConfidence = e.IdentityConfidence;
                    stable.IdentityMargin = e.IdentityMargin;
                    backfillMode = "UNKNOWN_ONLY";
                }
                else if (stable.StudentId == e.StudentId)
                {
                    if ((e.IdentityConfidence ?? 0) >= (stable.IdentityConfidence ?? 0))
                    {
                        stable.IdentityConfidence = e.IdentityConfidence;
                        stable.IdentityMargin = e.IdentityMargin;
                    }
                    stable.IdentityStatus = "IDENTIFIED";
                }
                else if (IsExplicitCorrection(e) && !candidateAlreadyClaimed)
                {
                    stable.StudentId = e.StudentId;
                    stable.IdentityStatus = "IDENTIFIED";
                    stable.IdentityConfidence = e.IdentityConfidence;
                    stable.IdentityMargin = e.IdentityMargin;
                    backfillMode = "ALL";
                }
                else
                {
                    // Không tự flip MSSV chỉ vì score mới cao hơn. Giữ assignment cũ và đánh dấu conflict.
                    stable.IdentityStatus = "CONFLICT";
                }
            }
            db.Update(stable);
        }

        if (!string.IsNullOrWhiteSpace(e.TrackId))
        {
            var seg = await db.TrackSegments.Where(x => x.StableIdentityId == stable.Id && x.TrackId == e.TrackId)
                .OrderByDescending(x => x.EndedAt).FirstOrDefaultAsync(ct);
            if (seg is null || (e.Timestamp - seg.EndedAt).TotalSeconds > 3)
            {
                await db.AddAsync(new TrackSegment { StableIdentityId = stable.Id, TrackId = e.TrackId, StartedAt = e.Timestamp, EndedAt = e.Timestamp, AvgQuality = e.ObservationQuality, ObservationCount = 1 }, ct);
            }
            else
            {
                seg.EndedAt = e.Timestamp > seg.EndedAt ? e.Timestamp : seg.EndedAt;
                var n = Math.Max(1, seg.ObservationCount);
                seg.AvgQuality = (((seg.AvgQuality ?? 0) * n) + e.ObservationQuality) / (n + 1);
                seg.ObservationCount = n + 1;
                db.Update(seg);
            }
        }

        if (e.StudentId is not null && e.IdentityConfidence is not null && e.EventType.StartsWith("IDENTITY", StringComparison.OrdinalIgnoreCase))
        {
            var decision = !candidateInRoster ? "OUT_OF_ROSTER" :
                candidateAlreadyClaimed && stable.StudentId != e.StudentId ? "DUPLICATE_CLAIM" :
                stable.StudentId == e.StudentId && stable.IdentityStatus == "IDENTIFIED" ? "ACCEPTED" :
                stable.IdentityStatus == "CONFLICT" ? "CONFLICT" : "EVIDENCE";
            await db.AddAsync(new IdentityLink { StableIdentityId = stable.Id, StudentId = e.StudentId, EvidenceType = "AI_IDENTITY", Score = Math.Clamp(e.IdentityConfidence.Value, 0, 1), Margin = e.IdentityMargin, Decision = decision, ModelVersion = e.ModelVersion }, ct);
        }

        if (backfillMode != "NONE" && stable.StudentId is not null)
        {
            var oldBehavior = await db.BehaviorEvents.Where(x => x.StableIdentityId == stable.Id && (backfillMode == "ALL" || x.StudentId == null)).ToListAsync(ct);
            foreach (var x in oldBehavior) { x.StudentId = stable.StudentId; db.Update(x); }
            var oldAlerts = await db.Alerts.Where(x => x.StableIdentityId == stable.Id && (backfillMode == "ALL" || x.StudentId == null)).ToListAsync(ct);
            foreach (var x in oldAlerts) { x.StudentId = stable.StudentId; db.Update(x); }
        }

        var accepted = false;
        if (!string.IsNullOrWhiteSpace(e.BehaviorLabel))
        {
            var label = e.BehaviorLabel.Trim().ToUpperInvariant();
            if (!BehaviorLabels.Contains(label)) throw new InvalidOperationException("BehaviorLabel không hợp lệ.");
            var start = e.StartedAt ?? e.Timestamp;
            var end = e.EndedAt ?? e.Timestamp;
            if (end <= start) throw new InvalidOperationException("BehaviorEvent phải có EndedAt > StartedAt; AI/EventEngine phải gửi segment đã làm mượt.");
            if ((end - start).TotalHours > 4) throw new InvalidOperationException("BehaviorEvent dài bất thường.");
            if (ExclusiveStateLabels.Contains(label))
            {
                var overlaps = await db.BehaviorEvents.AnyAsync(x => x.StableIdentityId == stable.Id
                    && ExclusiveStateLabels.Contains(x.Label)
                    && x.StartedAt < end && x.EndedAt > start, ct);
                if (overlaps) throw new InvalidOperationException("Behavior state segment bị chồng lấn trên cùng stable_id; EventEngine phải xuất timeline state loại trừ nhau.");
            }
            var behavior = new BehaviorEvent
            {
                ExternalEventId = e.EventId,
                SessionId = e.SessionId,
                StableIdentityId = stable.Id,
                StudentId = stable.StudentId,
                Label = label,
                Probability = Math.Clamp(e.BehaviorProbability ?? 0, 0, 1),
                StartedAt = start,
                EndedAt = end,
                ObservationQuality = e.ObservationQuality,
                ModelVersion = e.ModelVersion,
                ThresholdVersion = e.ThresholdVersion
            };
            await db.AddAsync(behavior, ct);
            await alertEngine.EvaluateAsync(behavior, ct);
            accepted = true;
        }
        else if (!string.IsNullOrWhiteSpace(e.AlertType))
        {
            // Hỗ trợ alert do AI phát trực tiếp nếu pipeline cần, nhưng mặc định ưu tiên Backend AlertEngine từ BehaviorEvent.
            var duration = Math.Max(0, e.AlertDurationSeconds ?? 0);
            await db.AddAsync(new Alert
            {
                ExternalEventId = e.EventId,
                SessionId = e.SessionId,
                StableIdentityId = stable.Id,
                StudentId = stable.StudentId,
                Type = e.AlertType.Trim().ToUpperInvariant(),
                StartedAt = e.StartedAt ?? e.Timestamp,
                EndedAt = e.EndedAt ?? e.Timestamp.AddSeconds(duration),
                DurationSeconds = duration,
                Confidence = Math.Clamp(e.BehaviorProbability ?? e.IdentityConfidence ?? 0, 0, 1),
                ObservationQuality = e.ObservationQuality,
                Status = "OPEN"
            }, ct);
            accepted = true;
        }
        else if (IsIdentityOrTrackEvent(e.EventType)) accepted = true;

        if (!accepted) return false;
        await db.AddAsync(new AiEventReceipt { ExternalEventId = e.EventId, SessionId = e.SessionId, EventType = e.EventType, EventTimestamp = e.Timestamp }, ct);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static bool IsLostEvent(string eventType) => eventType.Equals("TRACK_LOST", StringComparison.OrdinalIgnoreCase) || eventType.Equals("IDENTITY_LOST", StringComparison.OrdinalIgnoreCase);
    private static bool IsIdentityOrTrackEvent(string eventType) => eventType.StartsWith("IDENTITY", StringComparison.OrdinalIgnoreCase) || eventType.StartsWith("TRACK", StringComparison.OrdinalIgnoreCase);
    private static bool IsExplicitCorrection(AiEventEnvelope e) => e.EventType.Equals("IDENTITY_CORRECTION", StringComparison.OrdinalIgnoreCase) || string.Equals(e.IdentityDecision, "CORRECTION", StringComparison.OrdinalIgnoreCase);
}
