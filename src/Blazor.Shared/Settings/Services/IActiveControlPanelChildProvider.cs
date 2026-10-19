using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Settings.Services;

internal interface IActiveControlPanelChildProvider
{
    /// <returns>
    /// The active child of <paramref name="controlPanel"/>, or its first child when none is selected and it has no
    /// content of its own, otherwise <see langword="null"/>.
    /// </returns>
    IControlPanelRegistryItem? GetActiveChild(IControlPanelRegistryItem controlPanel);
}
