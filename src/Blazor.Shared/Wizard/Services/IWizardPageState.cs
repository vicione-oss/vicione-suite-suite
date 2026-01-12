using Blazor.Shared.Interfaces;
using Blazor.Shared.Wizard.Models;

namespace Blazor.Shared.Wizard.Services;

public interface IWizardPageState : IHasChangeableProperties
{
    /// <summary>
    /// Returns the current operation set in a call to <see cref="BeginOperation"/>, otherwise returns <see langword="null"/>.
    /// </summary>
    IWizardOperation? CurrentOperation { get; }

    /// <summary>
    /// Call this method to begin a operation cycle.
    /// 
    /// This increments the internal oepration counter, which initially is zero. The counter counts the number of times
    /// <see cref="BeginOperation"/> was called without a corresponding call to <see cref="EndOperation"/>.
    /// 
    /// If the internal operation counter was zero on method entry then <see cref="CurrentOperation" /> is set and
    /// the <see cref="Changed"/> event is raised.
    /// 
    /// Make sure to have a corresponding call to <see cref="EndOperation"/> to end the operation cycle.
    /// </summary>
    /// <param name="description">Optional description for the operation to allow the application to display something descriptive while the operation is running</param>
    /// <param name="estimatedDurationMs">Optional duration of the operation estimated in milliseconds to allow the application to know when the operation takes longer than expected</param>
    void BeginOperation(IWizardOperation operation);

    /// <summary>
    /// Call this method to end a operation cycle. This decrements the internal operation counter.
    /// 
    /// If the internal operation counter reaches zero then <see cref="CurrentOperation" /> is set to <see langword="null"/> and
    /// the <see cref="Changed"/> event is raised.
    /// </summary>
    void EndOperation();
}
