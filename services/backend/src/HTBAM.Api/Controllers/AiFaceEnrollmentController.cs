using HTBAM.Application.DTOs;
using HTBAM.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HTBAM.Api.Controllers;

[ApiController, Route("api/ai/face-enrollments")]
public sealed class AiFaceEnrollmentController(IFaceEnrollmentService enrollments, IConfiguration cfg) : ControllerBase
{
    [HttpPost("{enrollmentId:long}/result")]
    public async Task<ActionResult> Result(long enrollmentId, FaceEnrollmentAiResult result, CancellationToken ct)
    {
        if (!string.Equals(Request.Headers["X-AI-API-Key"], cfg["AiService:CallbackApiKey"], StringComparison.Ordinal)) return Unauthorized();
        await enrollments.ApplyAiResultAsync(enrollmentId, result, ct);
        return Ok(new { accepted = true });
    }
}
