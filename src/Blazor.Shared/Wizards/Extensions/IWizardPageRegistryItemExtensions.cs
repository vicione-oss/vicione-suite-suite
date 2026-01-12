using Blazor.Shared.Wizards.Comparers;
using Sdk.Client.Wizards.Services;

namespace Blazor.Shared.Wizards.Extensions;

internal static class IWizardPageRegistryItemExtensions
{
    public static IOrderedEnumerable<IWizardPageRegistryItem> Sort(this IEnumerable<IWizardPageRegistryItem> wizardpageRegistryItems)
        => wizardpageRegistryItems.OrderBy(wizardPageRegistryItem => wizardPageRegistryItem.Descriptor.Position, new OptionalPositionComparer());
}
