using FluentAssertions;
using Contacts.Domain.Entities;
using Contacts.Domain.Events;
using Contacts.Domain.ValueObjects;
using Xunit;

namespace Contacts.UnitTests.Domain;

public sealed class ContactTests
{
    private static Contact CreateValidContact() =>
        Contact.Create("Jane", "Doe", Email.From("jane.doe@example.com"));

    [Fact]
    public void Create_WithValidData_ShouldSetPropertiesCorrectly()
    {
        var contact = Contact.Create(
            "Jane", "Doe",
            Email.From("jane.doe@example.com"),
            PhoneNumber.From("+6421234567"),
            "Acme Ltd");

        contact.FirstName.Should().Be("Jane");
        contact.LastName.Should().Be("Doe");
        contact.Email.Value.Should().Be("jane.doe@example.com");
        contact.Phone!.Value.Should().Be("+6421234567");
        contact.Organisation.Should().Be("Acme Ltd");
        contact.FullName.Should().Be("Jane Doe");
        contact.IsDeleted.Should().BeFalse();
        contact.Id.Value.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_ShouldRaiseDomainEvent()
    {
        var contact = CreateValidContact();

        contact.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ContactCreatedEvent>();
    }

    [Theory]
    [InlineData("", "Doe", "jane@example.com")]
    [InlineData("Jane", "", "jane@example.com")]
    public void Create_WithMissingName_ShouldThrow(string first, string last, string email)
    {
        var act = () => Contact.Create(first, last, Email.From(email));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Update_ShouldModifyFieldsAndRaiseDomainEvent()
    {
        var contact = CreateValidContact();
        contact.ClearDomainEvents();

        contact.Update("John", "Smith", Email.From("john.smith@example.com"), null, "New Corp");

        contact.FirstName.Should().Be("John");
        contact.LastName.Should().Be("Smith");
        contact.Email.Value.Should().Be("john.smith@example.com");
        contact.Organisation.Should().Be("New Corp");
        contact.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ContactUpdatedEvent>();
    }

    [Fact]
    public void Delete_ShouldSetIsDeletedAndRaiseDomainEvent()
    {
        var contact = CreateValidContact();
        contact.ClearDomainEvents();

        contact.Delete();

        contact.IsDeleted.Should().BeTrue();
        contact.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ContactDeletedEvent>();
    }

    [Fact]
    public void Delete_CalledTwice_ShouldOnlyRaiseOneEvent()
    {
        var contact = CreateValidContact();
        contact.ClearDomainEvents();

        contact.Delete();
        contact.Delete();

        contact.DomainEvents.Should().ContainSingle();
    }
}
