namespace Blazor.Shared.Wizard.Models;

internal sealed class WizardPageInfo
{
    public required Type ComponentType { get; init; }
    public required Type StateType { get; init; }
    public required Type DescriptorType { get; set; }
    public required object KeyedServiceKey { get; init; }
}
