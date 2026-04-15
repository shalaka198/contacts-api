using FluentAssertions;
using Contacts.Application.Common;
using Xunit;

namespace Contacts.UnitTests.Application;

public sealed class ResultTests
{
    [Fact]
    public void Success_ShouldHaveIsSuccessTrue_AndValueAccessible()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Failure_ShouldHaveIsSuccessFalse_AndErrorAccessible()
    {
        var error = new Error("Test.Code", "Test description");
        var result = Result<int>.Failure(error);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Test.Code");
        result.Error.Description.Should().Be("Test description");
    }

    [Fact]
    public void Value_OnFailure_ShouldThrowInvalidOperationException()
    {
        var result = Result<int>.Failure(new Error("E", "D"));
        var act = () => result.Value;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Error_OnSuccess_ShouldThrowInvalidOperationException()
    {
        var result = Result<int>.Success(1);
        var act = () => result.Error;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Match_OnSuccess_ShouldInvokeOnSuccessBranch()
    {
        var result = Result<int>.Success(7);
        var output = result.Match(v => $"value:{v}", e => $"error:{e.Code}");
        output.Should().Be("value:7");
    }

    [Fact]
    public void Match_OnFailure_ShouldInvokeOnFailureBranch()
    {
        var result = Result<int>.Failure(new Error("X", "Y"));
        var output = result.Match(v => $"value:{v}", e => $"error:{e.Code}");
        output.Should().Be("error:X");
    }

    [Fact]
    public void ImplicitConversionFromError_ShouldCreateFailureResult()
    {
        Result<int> result = new Error("Implicit.Error", "Created via implicit operator");
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Implicit.Error");
    }
}
