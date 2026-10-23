using System.Collections.Frozen;
using Microsoft.AspNetCore.Identity;

namespace Blazor.Server.Backend.Extensions;

internal static class IdentityErrorExtensions
{
    private static readonly FrozenSet<string> s_passwordPolicyErrorCodes =
    [
        nameof(IdentityErrorDescriber.PasswordRequiresDigit),
        nameof(IdentityErrorDescriber.PasswordRequiresLower),
        nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric),
        nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars),
        nameof(IdentityErrorDescriber.PasswordRequiresUpper),
        nameof(IdentityErrorDescriber.PasswordTooShort),
    ];

    public static bool IsPasswordPolicyError(this IdentityError error)
        => s_passwordPolicyErrorCodes.Contains(error.Code);
}
