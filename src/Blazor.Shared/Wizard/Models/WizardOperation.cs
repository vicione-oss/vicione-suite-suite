namespace Blazor.Shared.Wizard.Models;

internal sealed class WizardOperation : IWizardOperation
{
    public string? Description { get; init; }
    public int? EstimatedDurationMs { get; init; }
}
