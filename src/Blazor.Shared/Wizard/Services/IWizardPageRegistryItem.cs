namespace Blazor.Shared.Wizard.Services;

public interface IWizardPageRegistryItem
{
    Type ComponentType { get; }
    IWizardPageDescriptor Descriptor { get; }
    IWizardPageState State { get; }
}
