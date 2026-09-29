using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.UserManagement.ControlPanels.OpenIdProvider.Services;

public sealed class OpenIdProviderControlPanelState : ControlPanelState
{
    internal string Authority { get; set; } = string.Empty;

    internal string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Empty keeps the stored secret, which never reaches the browser.
    /// </summary>
    internal string ClientSecret { get; set; } = string.Empty;

    internal bool ClientSecretStored { get; set; }

    /// <summary>
    /// The only way back to a public PKCE client, since an empty <see cref="ClientSecret"/> means keep.
    /// </summary>
    internal bool RemoveStoredClientSecret { get; set; }

    /// <summary>
    /// Replaces the form when loading failed, because an empty form would be saved as a removal.
    /// </summary>
    internal string? LoadError { get; set; }
}
