using HTBAM.Application.DTOs;

namespace HTBAM.Application.Interfaces;

public interface ILlmProvider
{
    string ProviderName { get; }
    string ModelName { get; }
    bool IsConfigured { get; }

    Task<LlmGenerationResult?> GenerateCommentAsync(
        LlmCommentRequest request,
        CancellationToken cancellationToken = default);
}

public interface ILlmProviderResolver
{
    ILlmProvider Resolve();
}

public interface IAutoCommentService
{
    Task<LlmGenerationResult?> GenerateSessionAsync(long sessionId, long? userId, CancellationToken cancellationToken = default);
    Task<LlmGenerationResult?> GenerateStudentAsync(long sessionId, long studentId, long? userId, CancellationToken cancellationToken = default);
    Task<LlmGenerationResult?> GenerateClassSectionAsync(long classSectionId, long? userId, CancellationToken cancellationToken = default);
}

