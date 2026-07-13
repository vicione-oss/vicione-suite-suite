using Sdk.Client.ControlPanels.Models;

namespace Blazor.Shared.Settings.Models;

internal sealed class NavigateBackOnSaveSuccessResult(string? message = null) : SaveSuccessResult(message);
