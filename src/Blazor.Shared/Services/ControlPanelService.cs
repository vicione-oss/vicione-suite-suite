using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Modules;

namespace Blazor.Shared.Services;

internal sealed class ControlPanelService(ILogger<ControlPanelService> logger) : IControlPanelService
{
    public bool IsDirty { get; private set; }

    public event Func<Task<ISaveResult>>? OnSave;

    public event Func<Task>? OnCancel;

    public event Func<Task>? OnBeginEdit;

    public event Func<Task>? OnCancelEdit;

    public async Task BeginEdit()
    {
        if (!IsDirty)
        {
            IsDirty = true;
            if (OnBeginEdit is not null)
                await OnBeginEdit.Invoke();
        }
    }

    public async Task CancelEdit()
    {
        if (OnCancel is not null)
            await OnCancel.Invoke();

        IsDirty = false;

        if (OnCancelEdit is not null)
            await OnCancelEdit.Invoke();
    }

    public async Task<ControlPanelStateFinishResult> FinishEdit()
    {
        try
        {
            if (OnSave is null)
                throw new ArgumentNullException(nameof(OnSave));

            var result = await OnSave.Invoke();

            IsDirty = result is SaveErrorResult;

            return new ControlPanelStateFinishResult(!IsDirty, result.Message);
        }
        catch (Exception e)
        {
            // We log the exception as warning because obsolete OnSave implementations in control panels
            // rely on exception flow to transport errors like validation errors to the UI.
            logger.LogWarning(e, "Exception was thrown");

            return new ControlPanelStateFinishResult(false, e.Message);
        }
    }
}
