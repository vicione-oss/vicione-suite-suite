using AwesomeAssertions;
using Blazor.Shared.Validation.Services.Validators;
using Xunit;

namespace Blazor.Shared.Tests.Validation.Services.Validators;

public sealed class EmailValidatorTests
{
    [Theory]
    [InlineData("user@example.com")]
    [InlineData("user.name@example.com")]
    [InlineData("user-name@example.com")]
    [InlineData("user_name@example.com")]
    [InlineData("user@sub.example.com")]
    [InlineData("user@example.co.uk")]
    [InlineData("user123@example.com")]
    public void Should_return_true_for_valid_email(string email)
    {
        // Act
        var result = new EmailValidator().Validate(email, "Email", out var errorMessage);

        // Assert
        result.Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Should_return_false_for_null_or_empty_email(string? email)
    {
        // Act
        var result = new EmailValidator().Validate(email, "Email", out var errorMessage);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("missing@dot")]
    [InlineData("@nodomain.com")]
    [InlineData("no-at-sign.com")]
    [InlineData(".leading-dot@example.com")]
    [InlineData("trailing-dot.@example.com")]
    [InlineData("user@.example.com")]
    [InlineData("user@example.")]
    public void Should_return_false_for_invalid_email(string email)
    {
        // Act
        var result = new EmailValidator().Validate(email, "Email", out var errorMessage);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Should_include_field_name_in_error_message()
    {
        // Arrange
        const string fieldName = "MyEmailField";

        // Act
        var result = new EmailValidator().Validate(null, fieldName, out var errorMessage);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().Contain(fieldName);
    }
}
