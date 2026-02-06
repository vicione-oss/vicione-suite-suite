using Blazor.Shared.Network.ControlPanels.RemoteAccess.Components;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Network.ControlPanels.RemoteAccess.Services;

internal sealed class RemoteAccessControlPanelDescriptor : IControlPanelDescriptor<RemoteAccessControlPanel>
{
    public string Category => CommonVocabulary.Network;
    public string Title => TechnicalTerms.RemoteAccess;
    public Uri? IconUrl => null;
}
