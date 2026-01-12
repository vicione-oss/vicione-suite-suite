using Sdk.Client.Wizards.Services;

namespace Blazor.Shared.Wizards.Models;

public readonly record struct WizardStep
{
    public required int Number { get; init; }
    public required string Title { get; init; }
    public required IWizardPageRegistryItem WizardPageRegistryItem { get; init; }
}
