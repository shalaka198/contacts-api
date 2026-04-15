using MediatR;
using Contacts.Application.Common;
using Contacts.Application.Interfaces;
using Contacts.Domain.Entities;
using Contacts.Domain.ValueObjects;

namespace Contacts.Application.Contacts.Commands.CreateContact;

public sealed class CreateContactCommandHandler : IRequestHandler<CreateContactCommand, Result<Guid>>
{
    private readonly IContactRepository _contacts;
    private readonly IUnitOfWork _unitOfWork;

    public CreateContactCommandHandler(IContactRepository contacts, IUnitOfWork unitOfWork)
    {
        _contacts = contacts;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(
        CreateContactCommand command,
        CancellationToken cancellationToken)
    {
        // Check for duplicate email before constructing the aggregate
        var existing = await _contacts.GetByEmailAsync(command.Email, cancellationToken);
        if (existing is not null)
            return Error.Conflict("Contact.DuplicateEmail",
                $"A contact with email '{command.Email}' already exists.");

        // Value objects validate their own invariants; propagate any ArgumentException as validation error
        Email email;
        PhoneNumber? phone = null;
        try
        {
            email = Email.From(command.Email);
            if (command.Phone is not null)
                phone = PhoneNumber.From(command.Phone);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("Input", ex.Message);
        }

        var contact = Contact.Create(
            command.FirstName,
            command.LastName,
            email,
            phone,
            command.Organisation);

        await _contacts.AddAsync(contact, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return Result<Guid>.Success(contact.Id.Value);
    }
}
