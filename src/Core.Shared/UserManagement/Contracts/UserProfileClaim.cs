using System.Security.Claims;

namespace Core.Shared.UserManagement.Contracts;

/// <summary>
/// Abstraction of a claim associated with a <see cref="UserProfile"/>
/// </summary>
/// <remarks>
/// This type was introduced because <see cref="Claim"/> is not serializable (required by MassTransit).
/// </remarks>
public readonly record struct UserProfileClaim
{
    /// <inheritdoc cref="Claim.Type"/>
    public required string Type { get; init; }

    /// <inheritdoc cref="Claim.Value"/>
    public required string Value { get; init; }
}
