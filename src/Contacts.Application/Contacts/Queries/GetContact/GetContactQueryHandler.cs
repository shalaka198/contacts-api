using MediatR;
using Contacts.Application.Common;
using Contacts.Application.Interfaces;
using Contacts.Domain.Common;
using Contacts.Domain.Entities;

namespace Contacts.Application.Contacts.Queries.GetContact;

public sealed class GetContactQueryHandler : IRequestHandler<GetContactQuery, Result<ContactDto>>
{
    private readonly IContactRepository _contacts;

    public GetContactQueryHandler(IContactRepository contacts) => _contacts = contacts;

    public async Task<Result<ContactDto>> Handle(GetContactQuery query, CancellationToken cancellationToken)
    {
        var id = ContactId.From(query.Id);
        var contact = await _contacts.GetByIdAsync(id, cancellationToken);

        if (contact is null)
            return Error.NotFound("Contact", query.Id);

        return Result<ContactDto>.Success(ToDto(contact));
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
