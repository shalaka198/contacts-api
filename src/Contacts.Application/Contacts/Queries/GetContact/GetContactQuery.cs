using MediatR;
using Contacts.Application.Common;

namespace Contacts.Application.Contacts.Queries.GetContact;

public sealed record GetContactQuery(Guid Id) : IRequest<Result<ContactDto>>;
