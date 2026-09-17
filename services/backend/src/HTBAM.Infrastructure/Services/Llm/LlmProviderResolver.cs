using HTBAM.Application.DTOs;
using HTBAM.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace HTBAM.Infrastructure.Services.Llm;

public sealed class LlmProviderResolver(
    IConfiguration configuration,
    GeminiLlmProvider gemini,
    UnavailableLlmProvider unavailable) : ILlmProviderResolver
{
    public ILlmProvider Resolve()
    {
        if (!configuration.GetValue("Llm:Enabled", true)) return unavailable;
        return (configuration["Llm:Provider"] ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "GEMINI" => gemini,
            _ => unavailable
        };
    }
}

public sealed class UnavailableLlmProvider(IConfiguration configuration) : ILlmProvider
{
    public string ProviderName => (configuration["Llm:Provider"] ?? "NOT_CONFIGURED").Trim().ToUpperInvariant();
    public string ModelName => Environment.GetEnvironmentVariable("GEMINI_MODEL") ?? configuration["Llm:Model"] ?? string.Empty;
    public bool IsConfigured => false;

    public Task<LlmGenerationResult?> GenerateCommentAsync(LlmCommentRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult<LlmGenerationResult?>(new LlmGenerationResult(string.Empty, ProviderName, ModelName, DateTime.UtcNow, false, "PROVIDER_UNAVAILABLE"));
}

