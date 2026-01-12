using Blazor.Shared.Wizard.Services;

namespace Blazor.Shared.Wizard.Models;

public readonly record struct WizardStep
{
    public required int Number { get; init; }
    public required string Title { get; init; }
    public required IWizardPageRegistryItem WizardPageRegistryItem { get; init; }
}
