namespace HTBAM.Application.DTOs;

public sealed record LlmCommentRequest(
    string SubjectType,
    string SubjectId,
    string SubjectName,
    decimal FocusedRatio,
    decimal DistractedRatio,
    decimal SleepyRatio,
    decimal ActiveRatio,
    int AlertCount,
    decimal ObservationQuality,
    decimal? AttendanceScore = null,
    int? PresentSeconds = null,
    int? ObservedSeconds = null,
    string? AdditionalContext = null);

public sealed record LlmGenerationResult(
    string Text,
    string Provider,
    string Model,
    DateTime GeneratedAt,
    bool IsFallback,
    string? ErrorCode = null);

