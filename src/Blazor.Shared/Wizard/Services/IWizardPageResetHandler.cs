namespace Blazor.Shared.Wizard.Services;

/// <summary>
/// Wizard page reset handler
/// </summary>
/// <typeparam name="TState">Type of the wizard page state</typeparam>
public interface IWizardPageResetHandler<TState>
    where TState : IWizardPageState
{
    /// <summary>
    /// Reset logic based on the given <paramref name="state"/> invoked when the wizard page associated with
    /// the given <paramref name="state"/> needs to be reset.
    /// </summary>
    /// <remarks>
    /// Unexpected behavior should be indicated by throwing an exception of any kind except <see cref="OperationCanceledException"/>,
    /// which indicates cancellation of the save operation.
    /// </remarks>
    /// <param name="state">Control panel state</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task Reset(TState state, CancellationToken cancellationToken);
}
