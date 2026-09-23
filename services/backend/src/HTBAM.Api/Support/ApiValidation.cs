namespace HTBAM.Api.Support;

public sealed record ApiFieldError(string Field, string Message);

public sealed record ApiErrorResponse(
    string Code,
    string Message,
    IReadOnlyList<ApiFieldError>? Errors = null);

public static class ApiValidation
{
    public static ApiErrorResponse Duplicate(
        string code,
        string field,
        string message) =>
        new(code, message, [new ApiFieldError(field, message)]);

    public static ApiErrorResponse DuplicateFields(
        IReadOnlyList<ApiFieldError> errors) =>
        new("DUPLICATE_FIELDS", "Có thông tin bị trùng.", errors);

    public static string Key(string value) => value.Trim().ToUpperInvariant();
}
