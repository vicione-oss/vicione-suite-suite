using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Profile.ControlPanels.ExternalIdProviders;

public sealed class ExternalIdProvidersControlPanelDescriptor : IControlPanelDescriptor<ExternalIdProvidersControlPanel>
{
    public string Title => Localization.ExternalIdProviders.Title;
    public int? Position => 1;
    public Uri? IconUrl => null;
}
