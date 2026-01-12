using Blazor.Shared.Wizard.Models;
using Microsoft.Extensions.Logging;

namespace Blazor.Shared.Wizard.Services;

internal sealed class WizardPageEditService<TContext>(ILogger<WizardPageEditService<TContext>> logger)
    : IWizardPageEditService<TContext>
{
    public bool IsDirty { get; private set; }

    public event Func<Task<ISaveResult>>? OnSave;
    public event Func<Task>? OnCancel;
    public event Func<Task>? OnBeginEdit;
    public event Func<Task>? OnCancelEdit;

    public async Task BeginEdit()
    {
        if (IsDirty)
            return;

        IsDirty = true;

        if (OnBeginEdit is not null)
            await OnBeginEdit.Invoke();
    }

    public async Task CancelEdit()
    {
        if (!IsDirty)
            return;

        if (OnCancel is not null)
            await OnCancel.Invoke();

        IsDirty = false;

        if (OnCancelEdit is not null)
            await OnCancelEdit.Invoke();
    }

    public async Task<WizardPageEditFinishResult> FinishEdit()
    {
        try
        {
            if (OnSave is null)
                throw new ArgumentNullException(nameof(OnSave));

            var result = await OnSave.Invoke();

            var success = result is not SaveErrorResult;

            if (IsDirty)
            {
                // Try to update dirty state only when it was set before as FinishEdit() might be called
                // when no edit was done before. In fact, no edit before FinishEdit() is an actual scenario
                // because OnSave executes validation _and_ save logic. So, when the user does not change
                // anything on the page, the page state should at least be validated.

                if (success)
                    IsDirty = false;
            }

            return new WizardPageEditFinishResult(success, result.Message);
        }
        catch (Exception e)
        {
            // We log the exception as warning because obsolete OnSave implementations in control panels
            // rely on exception flow to transport errors like validation errors to the UI.
            logger.LogWarning(e, $"Exception was thrown");

            return new WizardPageEditFinishResult(false, e.Message);
        }
    }

    public void Reset()
        => IsDirty = false;
}
