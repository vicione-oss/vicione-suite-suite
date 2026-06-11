using System.ComponentModel.DataAnnotations;
using PasskeyConstants = Core.Shared.Passkeys.Constants;

namespace Blazor.Shared.Profile.Models;

public sealed class PasskeyInputModel
{
    public string? CredentialJson { get; set; }

    [Required]
    [MaxLength(PasskeyConstants.MaxPasskeyNameLength)]
    public required string Name { get; init; }

    public string? Error { get; init; }
}
