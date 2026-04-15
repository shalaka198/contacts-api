using MediatR;
using Contacts.Application.Common;
using Contacts.Application.Interfaces;
using Contacts.Domain.Common;

namespace Contacts.Application.Contacts.Commands.DeleteContact;

public sealed class DeleteContactCommandHandler : IRequestHandler<DeleteContactCommand, Result>
{
    private readonly IContactRepository _contacts;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteContactCommandHandler(IContactRepository contacts, IUnitOfWork unitOfWork)
    {
        _contacts = contacts;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeleteContactCommand command, CancellationToken cancellationToken)
    {
        var id = ContactId.From(command.Id);
        var contact = await _contacts.GetByIdAsync(id, cancellationToken);

        if (contact is null)
            return Error.NotFound("Contact", command.Id);

        contact.Delete();
        _contacts.Update(contact);
        await _unitOfWork.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
