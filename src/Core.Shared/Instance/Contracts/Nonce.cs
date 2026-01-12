using System.ComponentModel.DataAnnotations;

namespace Core.Shared.Instance.Contracts;

/// <remarks>
/// <see href="https://en.wikipedia.org/wiki/Cryptographic_nonce"/>
/// </remarks>
public sealed class Nonce
{
    [Key]
    public required Guid Value { get; set; }

    [Required]
    public required DateTimeOffset CreatedAt { get; set; }
}
