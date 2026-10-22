using System.Diagnostics.CodeAnalysis;
using Core.Shared.Security;
using Core.Shared.UserManagement.Contracts;

namespace Blazor.Server.Backend.Services;

public sealed record ExternalUserCreationResult
{
    public SuiteUser? User { get; private init; }

    public ExternalLoginError Error { get; private init; }

    [MemberNotNullWhen(true, nameof(User))]
    public bool Succeeded => User is not null;

    public static ExternalUserCreationResult Success(SuiteUser user) => new() { User = user };

    public static ExternalUserCreationResult Failed(ExternalLoginError error) => new() { Error = error };
}
