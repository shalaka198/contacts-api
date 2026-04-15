namespace Contacts.Application.Common;

/// <summary>
/// Named error type used with the Result pattern.
/// Provides a machine-readable code for programmatic handling
/// and a human-readable description for logging/API responses.
/// </summary>
public sealed record Error(string Code, string Description)
{
    // Predefined common errors
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error NotFound(string resource, object id) =>
        new($"{resource}.NotFound", $"{resource} with ID '{id}' was not found.");

    public static Error Conflict(string code, string description) =>
        new(code, description);

    public static Error Validation(string field, string message) =>
        new($"Validation.{field}", message);

    public static Error Unauthorised() =>
        new("Auth.Unauthorised", "You are not authorised to perform this action.");

    public static Error Unexpected(string description) =>
        new("Unexpected", description);
}
