using FluentAssertions;
using Contacts.Domain.ValueObjects;
using Xunit;

namespace Contacts.UnitTests.Domain;

public sealed class EmailTests
{
    [Theory]
    [InlineData("user@example.com")]
    [InlineData("USER@EXAMPLE.COM")]
    [InlineData("user.name+tag@sub.domain.co.nz")]
    public void From_WithValidEmail_ShouldNormaliseToLowercase(string input)
    {
        var email = Email.From(input);
        email.Value.Should().Be(input.ToLowerInvariant());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("notanemail")]
    [InlineData("@nodomain.com")]
    [InlineData("noatsign.com")]
    public void From_WithInvalidEmail_ShouldThrowArgumentException(string input)
    {
        var act = () => Email.From(input);
        act.Should().Throw<ArgumentException>();
    }
}

public sealed class PhoneNumberTests
{
    [Theory]
    [InlineData("+6421234567")]
    [InlineData("+447911123456")]
    [InlineData("+12025550123")]
    public void From_WithValidE164_ShouldSucceed(string input)
    {
        var phone = PhoneNumber.From(input);
        phone.Value.Should().Be(input);
    }

    [Theory]
    [InlineData("021234567")]
    [InlineData("+1")]
    [InlineData("not-a-number")]
    public void From_WithInvalidFormat_ShouldThrow(string input)
    {
        var act = () => PhoneNumber.From(input);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void From_WithNullOrEmpty_ShouldReturnNoneWithNullValue()
    {
        PhoneNumber.From(string.Empty).Value.Should().BeNull();
        PhoneNumber.None().Value.Should().BeNull();
    }
}
