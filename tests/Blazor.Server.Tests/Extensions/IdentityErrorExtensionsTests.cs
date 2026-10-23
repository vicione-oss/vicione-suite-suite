using Blazor.Server.Backend.Extensions;
using Microsoft.AspNetCore.Identity;

namespace Blazor.Server.Tests.Extensions;

public sealed class IdentityErrorExtensionsTests
{
    private static readonly IdentityErrorDescriber s_describer = new();

    public static TheoryData<IdentityError> PasswordPolicyErrors =>
    [
        s_describer.PasswordRequiresDigit(),
        s_describer.PasswordRequiresLower(),
        s_describer.PasswordRequiresNonAlphanumeric(),
        s_describer.PasswordRequiresUniqueChars(3),
        s_describer.PasswordRequiresUpper(),
        s_describer.PasswordTooShort(12),
    ];

    public static TheoryData<IdentityError> OtherErrors =>
    [
        s_describer.PasswordMismatch(),
        s_describer.UserAlreadyHasPassword(),
        s_describer.DefaultError(),
    ];

    [Theory]
    [MemberData(nameof(PasswordPolicyErrors), DisableDiscoveryEnumeration = true)]
    public void Should_recognize_a_password_policy_error(IdentityError error)
    {
        // Act
        var isPasswordPolicyError = error.IsPasswordPolicyError();

        // Assert
        isPasswordPolicyError.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(OtherErrors), DisableDiscoveryEnumeration = true)]
    public void Should_not_take_other_errors_for_a_password_policy_error(IdentityError error)
    {
        // Act
        var isPasswordPolicyError = error.IsPasswordPolicyError();

        // Assert
        isPasswordPolicyError.Should().BeFalse();
    }
}
