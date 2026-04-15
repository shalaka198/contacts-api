using FluentAssertions;
using NSubstitute;
using Contacts.Application.Common;
using Contacts.Application.Contacts.Commands.CreateContact;
using Contacts.Application.Interfaces;
using Contacts.Domain.Entities;
using Contacts.Domain.ValueObjects;
using Xunit;

namespace Contacts.UnitTests.Application.Commands;

public sealed class CreateContactCommandHandlerTests
{
    private readonly IContactRepository _repository = Substitute.For<IContactRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CreateContactCommandHandler _handler;

    public CreateContactCommandHandlerTests()
    {
        _handler = new CreateContactCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldReturnSuccessWithNewId()
    {
        _repository.GetByEmailAsync("jane@example.com", Arg.Any<CancellationToken>())
            .Returns((Contact?)null);

        var command = new CreateContactCommand("Jane", "Doe", "jane@example.com", null, null);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        await _repository.Received(1).AddAsync(Arg.Any<Contact>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDuplicateEmail_ShouldReturnConflictError()
    {
        var existing = Contact.Create("Existing", "User", Email.From("jane@example.com"));
        _repository.GetByEmailAsync("jane@example.com", Arg.Any<CancellationToken>())
            .Returns(existing);

        var command = new CreateContactCommand("Jane", "Doe", "jane@example.com", null, null);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("DuplicateEmail");
        await _repository.DidNotReceive().AddAsync(Arg.Any<Contact>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInvalidEmail_ShouldReturnValidationError()
    {
        _repository.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Contact?)null);

        var command = new CreateContactCommand("Jane", "Doe", "not-an-email", null, null);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().StartWith("Validation");
    }

    [Fact]
    public async Task Handle_WithValidPhone_ShouldPersistPhoneNumber()
    {
        _repository.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Contact?)null);

        var command = new CreateContactCommand("Jane", "Doe", "jane@example.com", "+6421234567", null);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).AddAsync(
            Arg.Is<Contact>(c => c.Phone != null && c.Phone.Value == "+6421234567"),
            Arg.Any<CancellationToken>());
    }
}
