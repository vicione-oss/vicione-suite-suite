using Blazor.Shared.Wizard.Models;

namespace Blazor.Shared.Wizard.Services;

/// <summary>
/// Wizard page save handler
/// </summary>
/// <typeparam name="TState">Type of the wizard page state</typeparam>
public interface IWizardPageSaveHandler<TState>
    where TState : IWizardPageState
{
    /// <summary>
    /// Save logic based on the given <paramref name="state"/> invoked when the wizard page associated with
    /// the given <paramref name="state"/> needs to be saved.
    /// </summary>
    /// <remarks>
    /// Expected outcomes of the save operation should be indicated by the return value.
    /// 
    /// Unexpected behavior should be indicated by throwing an exception of any kind except <see cref="OperationCanceledException"/>,
    /// which indicates cancellation of the save operation.
    /// </remarks>
    /// <param name="state">Control panel state</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns><see cref="SaveSuccessResult"/> or <see cref="SaveErrorResult"/></returns>
    Task<ISaveResult> Save(TState state, CancellationToken cancellationToken);
}
