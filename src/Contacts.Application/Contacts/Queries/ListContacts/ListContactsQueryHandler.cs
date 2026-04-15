using MediatR;
using Contacts.Application.Common;
using Contacts.Application.Contacts.Queries;
using Contacts.Application.Interfaces;
using Contacts.Domain.Entities;

namespace Contacts.Application.Contacts.Queries.ListContacts;

public sealed class ListContactsQueryHandler
    : IRequestHandler<ListContactsQuery, Result<PagedResult<ContactDto>>>
{
    private readonly IContactRepository _contacts;

    public ListContactsQueryHandler(IContactRepository contacts) => _contacts = contacts;

    public async Task<Result<PagedResult<ContactDto>>> Handle(
        ListContactsQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var (items, totalCount) = await _contacts.ListAsync(
            query.SearchTerm, page, pageSize, cancellationToken);

        var dtos = items.Select(ToDto).ToList();

        return Result<PagedResult<ContactDto>>.Success(
            new PagedResult<ContactDto>(dtos, totalCount, page, pageSize));
    }

    private static ContactDto ToDto(Contact c) => new(
        c.Id.Value,
        c.FirstName,
        c.LastName,
        c.FullName,
        c.Email.Value,
        c.Phone?.Value,
        c.Organisation,
        c.CreatedAt,
        c.UpdatedAt);
}
