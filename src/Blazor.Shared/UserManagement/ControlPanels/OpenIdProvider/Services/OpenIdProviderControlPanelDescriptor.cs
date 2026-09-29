using Blazor.Shared.Enums;
using Blazor.Shared.Extensions;
using Blazor.Shared.UserManagement.ControlPanels.OpenIdProvider.Components;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.UserManagement.ControlPanels.OpenIdProvider.Services;

internal sealed class OpenIdProviderControlPanelDescriptor : IControlPanelDescriptor<OpenIdProviderControlPanel>
{
    public string Title => Localization.OpenIdProviderControlPanel.PanelTitle;
    public Uri IconUrl => SvgIcon.CloudConnection.GetPath();
}
