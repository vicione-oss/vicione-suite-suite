using Blazor.Shared.Models;
using Blazor.Shared.Wizard.Models;
using Blazor.Shared.Wizard.Services;
using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Wizard.Components;

public sealed partial class WizardPageContainer : ComponentBase, IDisposable
{
    private IWizardPageState _wizardPageState = default!;

    [Parameter, EditorRequired] public IWizardPageRegistryItem WizardPageRegistryItem { get; set; }
    [Parameter] public bool HasGenericLoadingOverlay { get; set; }
    [Parameter] public EventCallback<IWizardOperation> OnWizardOperationStarted { get; set; }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (_wizardPageState != WizardPageRegistryItem.State)
        {
            if (_wizardPageState is not null)
                _wizardPageState.Changed -= WizardPageStateChanged;

            _wizardPageState = WizardPageRegistryItem.State;

            _wizardPageState.Changed += WizardPageStateChanged;
        }
    }

    public void Dispose()
    {
        if (_wizardPageState is not null)
            _wizardPageState.Changed -= WizardPageStateChanged;
    }

    private async void WizardPageStateChanged(PropertiesChangedEventArgs args)
    {
        if (args.PropertyNames.Contains(nameof(IWizardPageState.CurrentOperation)))
        {
            if (args.Sender is IWizardPageState wizardPageState)
            {
                if (wizardPageState.CurrentOperation is not null && OnWizardOperationStarted.HasDelegate)
                    await OnWizardOperationStarted.InvokeAsync(wizardPageState.CurrentOperation);
            }

            await InvokeAsync(StateHasChanged);
        }
    }
}
