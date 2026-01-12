using Blazor.Shared.Wizards.Extensions;
using Blazor.Shared.Wizards.Factories;
using Blazor.Shared.Wizards.Models;
using Blazor.Shared.Wizards.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Client.Models;
using Sdk.Client.Wizards.Components;
using Sdk.Client.Wizards.Models;
using Sdk.Client.Wizards.Services;

namespace Blazor.Shared.Wizards.Components;

public sealed partial class WizardPageContainer : ComponentBase, IAsyncDisposable
{
    private bool _disposedAsync;

    private readonly Dictionary<string, object> _wizardPageParameters = [];

    private IWizardPageState? _wizardPageState;
    private IWizardPageEdit? _wizardPageEdit;

    [Parameter, EditorRequired] public IWizardPageRegistryItem WizardPageRegistryItem { get; set; }
    [Parameter, EditorRequired] public IWizardBodyContentRenderCycle WizardBodyContentRenderCycle { get; set; }
    [Parameter] public bool HasGenericLoadingOverlay { get; set; }
    [Parameter] public EventCallback<IWizardOperation> OnWizardOperationStarted { get; set; }

    [Inject] private IWizardPageEditRegistry WizardPageEditRegistry { get; set; } = default!;
    [Inject] private WizardPageEditFactory WizardPageEditFactory { get; set; } = default!;

    protected override void OnInitialized()
    {
        _wizardPageState = WizardPageRegistryItem.State;
        _wizardPageState.Changed += WizardPageStateChanged;

        _wizardPageParameters.Add(nameof(WizardPage<IWizardPageState>.State), _wizardPageState);

        _wizardPageParameters.Add(nameof(WizardPage<IWizardPageState>.OnBeginEdit),
            EventCallback.Factory.Create(this, WizardPageBeginEdit));

        _wizardPageParameters.Add(nameof(WizardPage<IWizardPageState>.OnCancelEdit),
            EventCallback.Factory.Create(this, WizardPageCancelEdit));

        _wizardPageParameters.Add(nameof(WizardPage<IWizardPageState>.OnAfterRenderCycle),
            EventCallback.Factory.Create<WizardPageAfterRenderCycleEventArgs>(this, WizardPageAfterRenderCycle));

        _wizardPageEdit = WizardPageEditFactory.CreateWizardPageEdit(_wizardPageState);
        WizardPageEditRegistry.Add(_wizardPageEdit);
    }

    protected override async Task OnInitializedAsync()
    {
        if (_disposedAsync)
            return;

        if (_wizardPageEdit is not null)
            await _wizardPageEdit.Reset();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposedAsync, true, false))
            return;

        // For now, we do not call CancelEdit because it would require an await on the async behavior
        // which could result in memory leaks when the await never returns

        if (_wizardPageState is not null)
            _wizardPageState.Changed -= WizardPageStateChanged;

        if (_wizardPageEdit is not null)
        {
            WizardPageEditRegistry.Remove(_wizardPageEdit);

            await _wizardPageEdit.DisposeAsync();
        }
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

    private void WizardPageBeginEdit()
        => _wizardPageEdit?.Begin();

    private async Task WizardPageCancelEdit()
    {
        if (_wizardPageEdit is null)
            return;

        await _wizardPageEdit.Cancel(withReset: true);
    }

    private void WizardPageAfterRenderCycle(WizardPageAfterRenderCycleEventArgs args)
    {
        if (!args.FirstRender)
            WizardBodyContentRenderCycle.SetFinished(WizardPageRegistryItem);
    }
}
