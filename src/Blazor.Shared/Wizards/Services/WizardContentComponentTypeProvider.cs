using Blazor.Shared.Wizards.Components;
using Sdk.Client.Wizards.Services;

namespace Blazor.Shared.Wizards.Services;

internal sealed class WizardContentComponentTypeProvider : IWizardContentComponentTypeProvider
{
    private Type? _wizardContentComponentType;

    public Type GetWizardContentComponentType<TContext>()
        => _wizardContentComponentType ??= typeof(WizardContent<TContext>);
}
