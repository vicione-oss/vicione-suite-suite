using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Services;

internal abstract class ControlPanelSaveHandlerBase<TState>(IUiMediator uiMediator) : CompletionSourceHandlerBase<ISaveResult>(uiMediator),
    IControlPanelSaveHandler<TState>
    where TState : class, IControlPanelState
{
    public abstract Task<ISaveResult> Save(TState state, CancellationToken cancellationToken);

    protected override ISaveResult CreateSuccessResult()
        => new SaveSuccessResult();

    protected override ISaveResult CreateErrorResult(string errorMessage, int? errorCode = null)
        => new SaveErrorResult(errorMessage ?? CommonPhrases.AnUnexpectedErrorOccurred, errorCode);
}
