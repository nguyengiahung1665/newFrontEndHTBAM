using HTBAM.Application.Interfaces; using HTBAM.Infrastructure.Data; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore;
namespace HTBAM.Api.Controllers;
[ApiController,Authorize,Route("api/system")]
public sealed class SystemController(AppDbContext db,IAiClient ai,IObjectStorage storage,IConfiguration cfg):ControllerBase
{
 [HttpGet("capabilities")]public async Task<ActionResult> Capabilities(CancellationToken ct){var aiHealth=await ai.HealthAsync(ct);var caps=aiHealth?await ai.CapabilitiesAsync(ct):new HTBAM.Application.DTOs.AiCapabilitiesResponse(false,false,Array.Empty<string>());return Ok(new{web=true,backend=true,database=await db.Database.CanConnectAsync(ct),objectStorage=await storage.HealthAsync(ct),aiHealth,sessionInference=caps.SessionInference,faceEnrollment=caps.FaceEnrollment,loadedModels=caps.LoadedModels,llmProvider=cfg["Llm:Provider"]??"NOT_CONFIGURED",modelIntegrationPending=!caps.SessionInference||!caps.FaceEnrollment});}
 [HttpGet("counts")]public async Task<ActionResult> Counts(CancellationToken ct)=>Ok(new{students=await db.StudentsSet.CountAsync(x=>x.IsActive,ct),classSections=await db.ClassSections.CountAsync(x=>x.IsActive,ct),sessions=await db.SessionsSet.CountAsync(ct),openAlerts=await db.AlertsSet.CountAsync(x=>x.Status=="OPEN",ct),cameras=await db.CamerasSet.CountAsync(x=>x.IsActive,ct),videos=await db.VideosSet.CountAsync(x=>x.Status=="READY",ct)});
}
