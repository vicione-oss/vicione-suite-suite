using Blazor.Shared.Enums;
using Blazor.Shared.Extensions;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Network.Services;

internal sealed class ControlPanelNetworkCategoryDescriptor : IControlPanelCategoryDescriptor
{
    public string Title => CommonVocabulary.Network;
    public string? IconCssClass => null;
    public Uri IconUrl => SvgIcon.HostConfig.GetPath();
    public int? Position => null;
}
