using Blazor.Shared.Enums;
using Blazor.Shared.Extensions;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Network.Services;

internal sealed class ControlPanelNetworkCategoryDescriptor : IControlPanelCategoryDescriptor
{
    public string Title => CommonVocabulary.Network;
    public string? IconCssClass => null;
    public Uri? IconUrl => new(SvgIcon.HostConfig.GetPath(), UriKind.Relative);
    public int? Position => null;
}
