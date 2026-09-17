using HTBAM.Application.Interfaces;
using HTBAM.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HTBAM.Api.Controllers;

[ApiController, Route("api/health")]
public sealed class HealthController(AppDbContext db, IAiClient ai, IObjectStorage storage) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> Get(CancellationToken ct)
    {
        var dbOk = await db.Database.CanConnectAsync(ct);
        var aiOk = await ai.HealthAsync(ct);
        var storageOk = await storage.HealthAsync(ct);
        return Ok(new { api = "ok", database = dbOk ? "ok" : "down", ai = aiOk ? "ok" : "down", objectStorage = storageOk ? "ok" : "down", utc = DateTime.UtcNow });
    }
}
