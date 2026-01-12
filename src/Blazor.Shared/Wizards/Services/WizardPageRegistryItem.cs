using Sdk.Client.Wizards.Services;

namespace Blazor.Shared.Wizards.Services;

internal sealed record WizardPageRegistryItem(Type ComponentType, IWizardPageDescriptor Descriptor, IWizardPageState State)
    : IWizardPageRegistryItem;
