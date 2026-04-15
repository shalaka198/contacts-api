namespace Contacts.Application.Contacts.Queries;

/// <summary>
/// Lightweight DTO returned from query handlers.
/// Kept in the Application layer — not the domain entity itself — so
/// the API surface can evolve independently of the domain model.
/// </summary>
public sealed record ContactDto(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string? Phone,
    string? Organisation,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
