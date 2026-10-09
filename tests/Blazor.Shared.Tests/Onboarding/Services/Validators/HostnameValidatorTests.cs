using Blazor.Shared.Onboarding.Services.Validators;
using ViciOne.Ui.Localization.Resources;
using HostnameValidatorMessages = Blazor.Shared.Onboarding.Services.Validators.Localization.HostnameValidator;

namespace Blazor.Shared.Tests.Onboarding.Services.Validators;

public sealed class HostnameValidatorTests
{
    private const string Field = "Hostname";

    private readonly HostnameValidator _validator = new();

    [Theory]
    [InlineData("edge")]
    [InlineData("edge-s-01")]
    [InlineData("EDGE01")]
    [InlineData("1")]
    public void Should_accept_alphanumeric_characters_and_inner_hyphens(string hostname)
    {
        // Act
        var result = _validator.Validate(hostname, Field, out var errorMessage);

        // Assert
        result.Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    [Fact]
    public void Should_reject_leading_hyphen()
    {
        // Act
        var result = _validator.Validate("-edge", Field, out var errorMessage);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().Be(string.Format(ValidationMessages.Culture, HostnameValidatorMessages.MustNotStartWithHypenCharacter, Field));
    }

    [Fact]
    public void Should_reject_trailing_hyphen()
    {
        // Act
        var result = _validator.Validate("edge-", Field, out var errorMessage);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().Be(string.Format(ValidationMessages.Culture, HostnameValidatorMessages.MustNotEndWithHypenCharacter, Field));
    }

    [Theory]
    [InlineData("edge.local")]
    [InlineData("edge_01")]
    [InlineData("edge 01")]
    [InlineData("edgé")]
    public void Should_reject_characters_other_than_alphanumerics_and_hyphens(string hostname)
    {
        // Act
        var result = _validator.Validate(hostname, Field, out var errorMessage);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().Be(string.Format(ValidationMessages.Culture,
            HostnameValidatorMessages.MustContainOnlyAlphanumericCharactersAndHyphens, Field));
    }
}
