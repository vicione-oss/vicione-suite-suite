using Blazor.Shared.Wizard.Comparers;
using Blazor.Shared.Wizard.Services;

namespace Blazor.Shared.Wizard.Extensions;

internal static class IWizardPageRegistryItemExtensions
{
    public static IOrderedEnumerable<IWizardPageRegistryItem> Sort(this IEnumerable<IWizardPageRegistryItem> wizardpageRegistryItems)
        => wizardpageRegistryItems.OrderBy(wizardPageRegistryItem => wizardPageRegistryItem.Descriptor.Position, new OptionalPositionComparer());
}
