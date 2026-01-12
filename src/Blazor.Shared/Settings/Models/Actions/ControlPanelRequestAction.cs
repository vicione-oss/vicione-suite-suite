using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Settings.Models.Actions;

internal record ControlPanelRequestAction(ControlPanelRequestedEventArgs Args) : IAction;
