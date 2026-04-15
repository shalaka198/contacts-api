using MediatR;
using Contacts.Application.Common;
using Contacts.Application.Interfaces;
using Contacts.Domain.Common;
using Contacts.Domain.ValueObjects;

namespace Contacts.Application.Contacts.Commands.UpdateContact;

public sealed class UpdateContactCommandHandler : IRequestHandler<UpdateContactCommand, Result>
{
    private readonly IContactRepository _contacts;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateContactCommandHandler(IContactRepository contacts, IUnitOfWork unitOfWork)
    {
        _contacts = contacts;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateContactCommand command, CancellationToken cancellationToken)
    {
        var id = ContactId.From(command.Id);
        var contact = await _contacts.GetByIdAsync(id, cancellationToken);

        if (contact is null)
            return Error.NotFound("Contact", command.Id);

        // If email is changing, check it's not already taken by another contact
        if (!contact.Email.Value.Equals(command.Email, StringComparison.OrdinalIgnoreCase))
        {
            var duplicate = await _contacts.GetByEmailAsync(command.Email, cancellationToken);
            if (duplicate is not null)
                return Error.Conflict("Contact.DuplicateEmail",
                    $"A contact with email '{command.Email}' already exists.");
        }

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

        contact.Update(command.FirstName, command.LastName, email, phone, command.Organisation);
        _contacts.Update(contact);
        await _unitOfWork.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
