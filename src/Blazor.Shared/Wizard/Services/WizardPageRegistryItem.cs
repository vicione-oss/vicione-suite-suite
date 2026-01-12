namespace Blazor.Shared.Wizard.Services;

internal sealed record WizardPageRegistryItem(Type ComponentType, IWizardPageDescriptor Descriptor, IWizardPageState State)
    : IWizardPageRegistryItem;
