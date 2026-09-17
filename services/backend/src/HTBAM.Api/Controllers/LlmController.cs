using HTBAM.Application.DTOs;
using HTBAM.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HTBAM.Api.Controllers;

[ApiController, Authorize, Route("api/llm")]
public sealed class LlmController(IConfiguration configuration, ILlmProviderResolver providerResolver) : ControllerBase
{
    [HttpGet("status")]
    public ActionResult Status()
    {
        var enabled = configuration.GetValue("Llm:Enabled", true);
        var provider = providerResolver.Resolve();
        return Ok(new
        {
            enabled,
            provider = (configuration["Llm:Provider"] ?? provider.ProviderName).ToUpperInvariant(),
            model = Environment.GetEnvironmentVariable("GEMINI_MODEL") ?? configuration["Llm:Model"] ?? provider.ModelName,
            configured = enabled && provider.IsConfigured,
            fallbackAvailable = true
        });
    }

    [HttpPost("test"), Authorize(Roles = "ADMIN,TECH_AI")]
    public async Task<ActionResult> Test(CancellationToken cancellationToken)
    {
        var enabled = configuration.GetValue("Llm:Enabled", true);
        var provider = providerResolver.Resolve();
        if (!enabled || !provider.IsConfigured)
            return Ok(new { provider = provider.ProviderName, model = provider.ModelName, status = "NOT_CONFIGURED", success = false });

        var result = await provider.GenerateCommentAsync(new LlmCommentRequest(
            "SYSTEM_TEST", "0", "SYSTEM_TEST", 0, 0, 0, 0, 0, 1,
            AdditionalContext: "Kiểm tra kết nối; trả lời thật ngắn."), cancellationToken);
        var success = result is not null && string.IsNullOrWhiteSpace(result.ErrorCode) && !string.IsNullOrWhiteSpace(result.Text);
        return Ok(new
        {
            provider = provider.ProviderName,
            model = provider.ModelName,
            status = success ? "AVAILABLE" : result?.ErrorCode ?? "UNAVAILABLE",
            success
        });
    }
}
