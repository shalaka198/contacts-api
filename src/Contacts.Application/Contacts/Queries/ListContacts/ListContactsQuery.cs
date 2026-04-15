using MediatR;
using Contacts.Application.Common;

namespace Contacts.Application.Contacts.Queries.ListContacts;

public sealed record ListContactsQuery(
    string? SearchTerm,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<ContactDto>>>;
