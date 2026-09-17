using HTBAM.Infrastructure.Data; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore;
namespace HTBAM.Api.Controllers;
[ApiController,Authorize(Roles="ADMIN"),Route("api/audit")]
public sealed class AuditController(AppDbContext db):ControllerBase
{
 [HttpGet]public async Task<ActionResult> List([FromQuery]string? action,[FromQuery]long? userId,[FromQuery]DateTime? from,[FromQuery]DateTime? to,[FromQuery]int take=500,CancellationToken ct=default){var q=db.AuditLogsSet.AsNoTracking().AsQueryable();if(!string.IsNullOrWhiteSpace(action))q=q.Where(x=>x.Action.Contains(action));if(userId!=null)q=q.Where(x=>x.UserId==userId);if(from!=null)q=q.Where(x=>x.CreatedAt>=from);if(to!=null)q=q.Where(x=>x.CreatedAt<to);return Ok(await q.OrderByDescending(x=>x.CreatedAt).Take(Math.Clamp(take,1,2000)).ToListAsync(ct));}
}
