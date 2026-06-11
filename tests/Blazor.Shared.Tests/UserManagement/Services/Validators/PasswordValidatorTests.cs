using AwesomeAssertions;
using Blazor.Shared.UserManagement.Services.Validators;
using Blazor.Shared.Validation.Services.Validators;
using Xunit;

namespace Blazor.Shared.Tests.UserManagement.Services.Validators;

public sealed class PasswordValidatorTests
{
    [Theory]
    [InlineData("ValidPass1!!")]
    [InlineData("Abcdefghij1!")]
    [InlineData("MyP@ssw0rd12")]
    [InlineData("ValidPass1__")]
    public void Should_return_true_for_valid_password(string password)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = sut.Validate(password, "Password", out var errorMessage);

        // Assert
        result.Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    [Fact]
    public void Should_return_true_for_password_at_minimum_length()
    {
        // Arrange
        var sut = CreateSut();
        const string password = "ValidPass1!a"; // exactly 12 characters

        // Act
        var result = sut.Validate(password, "Password", out var errorMessage);

        // Assert
        result.Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    [Fact]
    public void Should_return_false_for_null_password()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = sut.Validate(null!, "Password", out var errorMessage);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().NotBeNullOrEmpty();
        errorMessage.Should().Contain("Password");
    }

    [Fact]
    public void Should_return_false_for_empty_password()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = sut.Validate(string.Empty, "Password", out var errorMessage);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().NotBeNullOrEmpty();
        errorMessage.Should().Contain("Password");
    }

    [Theory]
    [InlineData("Ab1!")]
    [InlineData("ValidP1!")]
    public void Should_return_false_when_password_is_too_short(string password)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = sut.Validate(password, "Password", out var errorMessage);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().NotBeNullOrEmpty();
        errorMessage.Should().Contain("Password");
        errorMessage.Should().Contain("12");
    }

    [Fact]
    public void Should_return_false_when_password_has_no_uppercase()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = sut.Validate("invalidpass1!xx", "Password", out var errorMessage);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Should_return_false_when_password_has_no_lowercase()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = sut.Validate("INVALIDPASS1!XX", "Password", out var errorMessage);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Should_return_false_when_password_has_no_digit()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = sut.Validate("InValidPassword!", "Password", out var errorMessage);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Should_return_true_when_underscore_is_the_only_special_character()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = sut.Validate("ValidPass1_xx", "Password", out var errorMessage);

        // Assert
        result.Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    [Fact]
    public void Should_return_false_when_password_has_no_special_character()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = sut.Validate("InValidPassword1", "Password", out var errorMessage);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Should_include_field_name_in_error_message_when_too_short()
    {
        // Arrange
        var sut = CreateSut();
        const string fieldName = "MyPasswordField";

        // Act
        var result = sut.Validate("Ab1!", fieldName, out var errorMessage);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().Contain(fieldName);
    }

    private static PasswordValidator CreateSut() => new(new RequiredValidator());
}
