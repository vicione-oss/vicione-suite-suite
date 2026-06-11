using Blazor.Shared.UserManagement.Services;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Profile.ControlPanels.ExternalIdProviders;

public sealed class ExternalIdProvidersControlPanelState : ControlPanelState
{
    public bool IsExternalAuthenticationProviderConfigured { get; set; }

    public ExternalUserAccount? LinkedExternalAccount { get; set; }

    public string? RemovalError { get; set; }
}
