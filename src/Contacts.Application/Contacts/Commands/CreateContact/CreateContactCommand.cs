using MediatR;
using Contacts.Application.Common;

namespace Contacts.Application.Contacts.Commands.CreateContact;

/// <summary>
/// Command to create a new contact.
/// Commands are write operations; they return a Result wrapping the new resource ID.
/// </summary>
public sealed record CreateContactCommand(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? Organisation) : IRequest<Result<Guid>>;
