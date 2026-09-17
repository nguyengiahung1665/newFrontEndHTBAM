using System.Text.Json;
using HTBAM.Application.Interfaces;
using HTBAM.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HTBAM.Application.Services;

public sealed class AuditService(IAppDbContext db) : IAuditService
{
    public async Task WriteAsync(long? userId, string action, string entityType, string entityId, object? metadata = null, CancellationToken ct = default)
    {
        await db.AddAsync(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            MetadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata)
        }, ct);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class AlertEngine(IAppDbContext db) : IAlertEngine
{
    public async Task<IReadOnlyList<Alert>> EvaluateAsync(BehaviorEvent evt, CancellationToken ct)
    {
        var durationSeconds = Math.Max(0, (int)Math.Round((evt.EndedAt - evt.StartedAt).TotalSeconds));
        var rules = await db.AlertRules
            .Where(x => x.Enabled && x.BehaviorLabel == evt.Label)
            .ToListAsync(ct);

        var created = new List<Alert>();
        foreach (var rule in rules)
        {
            if (durationSeconds < rule.MinDurationSeconds || evt.Probability < rule.MinConfidence || evt.ObservationQuality < rule.MinObservationQuality)
                continue;

            var externalId = $"{evt.ExternalEventId}:{rule.Code}";
            if (externalId.Length > 128) externalId = externalId[..128];
            if (await db.Alerts.AnyAsync(x => x.ExternalEventId == externalId, ct)) continue;

            var alert = new Alert
            {
                ExternalEventId = externalId,
                SessionId = evt.SessionId,
                StableIdentityId = evt.StableIdentityId,
                StudentId = evt.StudentId,
                Type = rule.Code,
                StartedAt = evt.StartedAt,
                EndedAt = evt.EndedAt,
                DurationSeconds = durationSeconds,
                Confidence = evt.Probability,
                ObservationQuality = evt.ObservationQuality,
                Status = "OPEN"
            };
            await db.AddAsync(alert, ct);
            created.Add(alert);
        }
        return created;
    }
}
