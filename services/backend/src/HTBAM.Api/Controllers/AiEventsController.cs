using HTBAM.Application.DTOs; using HTBAM.Application.Interfaces; using HTBAM.Api.Hubs; using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.SignalR;
namespace HTBAM.Api.Controllers;
[ApiController,Route("api/ai/events")]
public sealed class AiEventsController(IAiEventService events,IHubContext<SessionHub> hub,IConfiguration cfg):ControllerBase
{
 [HttpPost] public async Task<ActionResult> Receive(AiEventEnvelope evt,CancellationToken ct){if(Request.Headers["X-AI-API-Key"]!=cfg["AiService:CallbackApiKey"])return Unauthorized();var inserted=await events.ProcessAsync(evt,ct);if(inserted)await hub.Clients.Group($"session:{evt.SessionId}").SendAsync("aiEvent",evt,ct);return Ok(new{accepted=inserted});}
}
