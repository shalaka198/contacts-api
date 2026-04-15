using FluentAssertions;
using NSubstitute;
using Contacts.Application.Common;
using Contacts.Application.Contacts.Queries;
using Contacts.Application.Contacts.Queries.GetContact;
using Contacts.Application.Interfaces;
using Contacts.Domain.Common;
using Contacts.Domain.Entities;
using Contacts.Domain.ValueObjects;
using Xunit;

namespace Contacts.UnitTests.Application.Queries;

public sealed class GetContactQueryHandlerTests
{
    private readonly IContactRepository _repository = Substitute.For<IContactRepository>();
    private readonly GetContactQueryHandler _handler;

    public GetContactQueryHandlerTests()
    {
        _handler = new GetContactQueryHandler(_repository);
    }

    [Fact]
    public async Task Handle_WithExistingContact_ShouldReturnDto()
    {
        var contact = Contact.Create("Jane", "Doe", Email.From("jane@example.com"),
            PhoneNumber.From("+6421234567"), "Acme");

        _repository.GetByIdAsync(Arg.Any<ContactId>(), Arg.Any<CancellationToken>())
            .Returns(contact);

        var result = await _handler.Handle(
            new GetContactQuery(contact.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<ContactDto>()
            .Which.Email.Should().Be("jane@example.com");
        result.Value.FullName.Should().Be("Jane Doe");
    }

    [Fact]
    public async Task Handle_WithNonExistentId_ShouldReturnNotFoundError()
    {
        _repository.GetByIdAsync(Arg.Any<ContactId>(), Arg.Any<CancellationToken>())
            .Returns((Contact?)null);

        var result = await _handler.Handle(
            new GetContactQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }
}
