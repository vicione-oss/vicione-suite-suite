using Sdk.Client.ControlPanels.Models;

namespace Blazor.Shared.Network.Models;

internal sealed class SaveInternalErrorResult(string message, int? errorCode = null)
    : SaveErrorResult(message, errorCode), ISaveInternalResult;
