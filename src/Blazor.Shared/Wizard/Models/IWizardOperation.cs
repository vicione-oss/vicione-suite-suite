namespace Blazor.Shared.Wizard.Models;

public interface IWizardOperation
{
    /// <summary>
    /// Description for the operation
    /// </summary>
    /// <remarks>
    /// The application will use this property to display something descriptive while the operation is running.
    /// </remarks>
    string? Description { get; }

    /// <summary>
    /// Duration of the operation estimated in milliseconds
    /// </summary>
    /// <remarks>
    /// The application will use this property to detect when the operation takes longer than expected.
    /// </remarks>
    int? EstimatedDurationMs { get; }
}
