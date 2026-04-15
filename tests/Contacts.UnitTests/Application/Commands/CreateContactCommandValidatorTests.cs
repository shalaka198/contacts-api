using FluentAssertions;
using FluentValidation.TestHelper;
using Contacts.Application.Contacts.Commands.CreateContact;
using Xunit;

namespace Contacts.UnitTests.Application.Commands;

public sealed class CreateContactCommandValidatorTests
{
    private readonly CreateContactCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_ShouldHaveNoErrors()
    {
        var command = new CreateContactCommand("Jane", "Doe", "jane@example.com", "+6421234567", "Acme");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyFirstName_ShouldHaveError(string firstName)
    {
        var command = new CreateContactCommand(firstName, "Doe", "jane@example.com", null, null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.FirstName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    public void Validate_WithInvalidEmail_ShouldHaveError(string email)
    {
        var command = new CreateContactCommand("Jane", "Doe", email, null, null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Theory]
    [InlineData("021234567")]    // Missing + prefix
    [InlineData("+1")]           // Too short
    [InlineData("(02) 123 4567")]// Local format
    public void Validate_WithNonE164Phone_ShouldHaveError(string phone)
    {
        var command = new CreateContactCommand("Jane", "Doe", "jane@example.com", phone, null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.Phone);
    }

    [Fact]
    public void Validate_WithNullPhone_ShouldHaveNoPhoneError()
    {
        var command = new CreateContactCommand("Jane", "Doe", "jane@example.com", null, null);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(c => c.Phone);
    }

    [Fact]
    public void Validate_WithOversizedOrganisation_ShouldHaveError()
    {
        var command = new CreateContactCommand("Jane", "Doe", "jane@example.com", null, new string('X', 201));
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.Organisation);
    }
}
