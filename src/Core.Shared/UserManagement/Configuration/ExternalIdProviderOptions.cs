using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Core.Shared.UserManagement.Configuration;

public record ExternalIdProviderOptions
{
    public const string ConfigSection = "ExternalIdProviders";

    [ValidateEnumeratedItems]
    public required IList<ExternalIdProvider> Providers { get; init; } = [];
}

public record ExternalIdProvider
{
    [Required] public required string Name { get; init; }

    [Required, Url] public required string Authority { get; init; }

    [Required] public required string ClientId { get; init; }

    [Required] public required string ClientSecret { get; init; }
}
