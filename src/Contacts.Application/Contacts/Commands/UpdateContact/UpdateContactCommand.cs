using MediatR;
using Contacts.Application.Common;

namespace Contacts.Application.Contacts.Commands.UpdateContact;

public sealed record UpdateContactCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? Organisation) : IRequest<Result>;
