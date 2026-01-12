using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Settings.Models;

internal sealed record ControlPanelPageRegistryItem(
    IControlPanelPage ControlPanelPage, IControlPanelRegistryItem ControlPanelRegistryItem) : IControlPanelPageRegistryItem;
