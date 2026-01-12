using System.Globalization;
using Blazor.Shared.Models;
using Blazor.Shared.Popup.Services;
using Blazor.Shared.Wizard.Extensions;
using Blazor.Shared.Wizard.Factories;
using Blazor.Shared.Wizard.Models;
using Blazor.Shared.Wizard.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Sdk.Client.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Wizard.Components;

public sealed partial class WizardBody<TContext> : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly SemaphoreSlim _updateStepsSemaphore = new(1);
    private readonly SemaphoreSlim _stepsUpdatedSemaphore = new(1);

    private bool _initialized;
    private bool _disposed;

    private bool _requestedNavigateBack;

    private WizardBodyContentRenderCycle _contentRenderCycle = new();

    [Inject] private ILogger<WizardBody<TContext>> Logger { get; set; } = default!;
    [Inject] private IWizardState<TContext> State { get; set; } = default!;
    [Inject] private IWizardPageRegistry<TContext> PageRegistry { get; set; } = default!;
    [Inject] private IWizardStepFactory StepFactory { get; set; } = default!;
    [Inject] private IWizardPageEditService<TContext> PageEditService { get; set; } = default!;
    [Inject] private ILoadingIndicationPlacementBehavior LoadingIndicationPlacementBehavior { get; set; } = default!;

    [Parameter, EditorRequired] public string Title { get; set; }
    [Parameter] public bool AllowExit { get; set; }
    [Parameter] public EventCallback OnExit { get; set; }
    [Parameter] public EventCallback OnHasUnsavedChanges { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var wizardPageRegistryItems = PageRegistry.ToArray();

        State.Changed += StateChanged;

        await UpdateSteps(resetActiveStep: true);

        PageRegistry.Changed += PageRegistryChanged;

        PageEditService.OnBeginEdit += PageEditServiceBeginOrCancelEdit;
        PageEditService.OnCancelEdit += PageEditServiceBeginOrCancelEdit;

        LoadingIndicationPlacementBehavior.PlacementChanged += LoadingIndicationPlacementChanged;

        _initialized = true;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        LoadingIndicationPlacementBehavior.PlacementChanged -= LoadingIndicationPlacementChanged;

        PageEditService.OnCancelEdit -= PageEditServiceBeginOrCancelEdit;
        PageEditService.OnBeginEdit -= PageEditServiceBeginOrCancelEdit;

        PageRegistry.Changed -= PageRegistryChanged;

        State.Changed -= StateChanged;

        _contentRenderCycle.Completed -= ContentRenderCycleCompleted;

        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();

        _updateStepsSemaphore.Dispose();
        _stepsUpdatedSemaphore.Dispose();

        _disposed = true;
    }

    private async void PageRegistryChanged(RegistryChangedEventArgs<IWizardPageRegistryItem> args)
    {
        var resetActiveStep = ShouldResetActiveStep(args);

        if (resetActiveStep)
            await UpdateSteps(resetActiveStep);
    }

    private bool ShouldResetActiveStep(RegistryChangedEventArgs<IWizardPageRegistryItem> args)
    {
        var activeStep = State.ActiveStep;
        if (activeStep is null)
            return true;

        if (args.ItemsRemoved.Any())
        {
            var stepMap = State.Steps.ToDictionary(k => k.WizardPageRegistryItem);

            foreach (var itemRemoved in args.ItemsRemoved)
            {
                if (stepMap.TryGetValue(itemRemoved, out var stepRemoved))
                {
                    if (stepRemoved.Number <= activeStep.Value.Number)
                        return true;
                }
            }
        }

        if (args.ItemsAdded.Any())
        {
            var wizardPageRegistryItems = PageRegistry.ToArray();
            var steps = CreateWizardSteps(wizardPageRegistryItems);
            var stepMap = steps.ToDictionary(k => k.WizardPageRegistryItem);

            foreach (var itemAdded in args.ItemsAdded)
            {
                if (stepMap.TryGetValue(itemAdded, out var stepAdded))
                {
                    if (stepAdded.Number <= activeStep.Value.Number)
                        return true;
                }
            }
        }

        return false;
    }

    private async void StateChanged(PropertiesChangedEventArgs args)
    {
        var stateHasChanged = false;

        if (args.PropertyNames.Contains(nameof(State.Steps)) ||
            args.PropertyNames.Contains(nameof(State.ActiveStep)))
        {
            try
            {
                var cancellationToken = _cancellationTokenSource.Token;

                await _stepsUpdatedSemaphore.WaitAsync(cancellationToken);

                try
                {
                    // Here we are in a situation where step states have already been changed,
                    // which means UI elements like stepper or the content area will most likely
                    // render different elements compare to before, so we handle this statewise
                    // as start from scratch. This means we need to reset the PageEditService to
                    // keep the dirty state in sync and update other dependencies as well.
                    PageEditService.Reset();

                    _contentRenderCycle.Completed -= ContentRenderCycleCompleted;
                    _contentRenderCycle = new(State.ActiveStep.HasValue ? [State.ActiveStep.Value.WizardPageRegistryItem] : []);
                    _contentRenderCycle.Completed += ContentRenderCycleCompleted;

                    State.BeginUpdate();
                    try
                    {
                        UpdateContentActionButtonStates();

                        State.LastSaveResult = null;
                    }
                    finally
                    {
                        State.EndUpdate();
                    }

                    stateHasChanged = true;
                }
                finally
                {
                    _stepsUpdatedSemaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                // Nothing to do here, return gracefully
            }
            catch (ObjectDisposedException)
            {
                // Semaphore or CancellationTokenSource already disposed, nothing we can do, return gracefully
            }
        }

        if (args.PropertyNames.Contains(nameof(State.BackButtonEnabled)) ||
            args.PropertyNames.Contains(nameof(State.NextButtonEnabled)) ||
            args.PropertyNames.Contains(nameof(State.FinishButtonEnabled)) ||
            args.PropertyNames.Contains(nameof(State.LastSaveResult)))
        {
            stateHasChanged = true;
        }

        if (args.PropertyNames.Contains(nameof(State.LoadingOverlayVisible)))
        {
            try
            {
                var cancellationToken = _cancellationTokenSource.Token;

                if (State.LoadingOverlayVisible)
                    await LoadingIndicationPlacementBehavior.Attach(cancellationToken);
                else
                    await LoadingIndicationPlacementBehavior.Remove(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Nothing to do here, return gracefully
            }
            catch (ObjectDisposedException)
            {
                // CancellationTokenSource already disposed, nothing we can do, return gracefully
            }

            stateHasChanged = true;
        }

        if (stateHasChanged)
            await InvokeAsync(StateHasChanged);
    }

    private void ContentRenderCycleCompleted()
        => UpdateContentActionButtonStates();

    private async Task WizardOperationStarted(IWizardOperation wizardOperation)
    {
        var placementTransitionIntervalMs = LoadingIndicationPlacementBehavior.DefaultPlacementTransitionIntervalMs;

        if (wizardOperation.EstimatedDurationMs is not null)
            placementTransitionIntervalMs = Math.Max(wizardOperation.EstimatedDurationMs.Value, placementTransitionIntervalMs);

        try
        {
            var cancellationToken = _cancellationTokenSource.Token;

            await LoadingIndicationPlacementBehavior.Adjust(placementTransitionIntervalMs, cancellationToken);
        }
        catch (ObjectDisposedException)
        {
            // CancellationTokenSource already disposed, nothing we can do, return gracefully
        }
    }

    private async void LoadingIndicationPlacementChanged()
        => await InvokeAsync(StateHasChanged);

    private async Task UpdateSteps(bool resetActiveStep = false)
    {
        try
        {
            var cancellationToken = _cancellationTokenSource.Token;

            await _updateStepsSemaphore.WaitAsync(cancellationToken);

            try
            {
                var wizardPageRegistryItems = PageRegistry.ToArray();

                UpdateStepStates(wizardPageRegistryItems, resetActiveStep);
            }
            finally
            {
                _updateStepsSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // nothing to do here, we return gracefully
        }
        catch (ObjectDisposedException)
        {
            // Semaphore, CancellationTokenSource or other object already disposed, nothing we can do, return gracefully
        }
    }

    private void UpdateStepStates(IWizardPageRegistryItem[] wizardPageRegistryItems, bool resetActiveStep)
    {
        State.BeginUpdate();
        try
        {
            State.Steps = CreateWizardSteps(wizardPageRegistryItems);

            if (resetActiveStep)
                State.ActiveStep = State.Steps.Count > 0 ? State.Steps[0] : null;
            else
                State.ActiveStep = State.Steps.FirstOrDefault(s => s.WizardPageRegistryItem == State.ActiveStep?.WizardPageRegistryItem);
        }
        finally
        {
            State.EndUpdate();
        }
    }

    private void UpdateContentActionButtonStates()
    {
        State.BeginUpdate();
        try
        {
            if (!_contentRenderCycle.Complete)
            {
                State.BackButtonEnabled = false;
                State.NextButtonEnabled = false;
                State.FinishButtonEnabled = false;

                return;
            }

            var activeStep = State.ActiveStep;

            State.BackButtonEnabled = activeStep?.Number > 1;
            State.NextButtonEnabled = activeStep?.Number < State.Steps.Count;
            State.FinishButtonEnabled = activeStep?.Number >= 1 && activeStep?.Number == State.Steps.Count;
        }
        finally
        {
            State.EndUpdate();
        }
    }

    private List<WizardStep> CreateWizardSteps(IEnumerable<IWizardPageRegistryItem> wizardPageRegistryItems)
        => [.. StepFactory.CreateAll(wizardPageRegistryItems.Sort())];

    private async Task BackButtonClick()
    {
        if (!_contentRenderCycle.Complete)
            return;

        if (PageEditService.IsDirty)
        {
            _requestedNavigateBack = true;

            await OnHasUnsavedChanges.InvokeAsync();
        }
        else
        {
            NavigateBack();
        }
    }

    private void NavigateBack()
    {
        if (!State.ActiveStep.HasValue)
            return;

        var activeStepIndex = State.ActiveStep.Value.Number - 1;

        State.ActiveStep = State.Steps.Skip(activeStepIndex - 1).FirstOrDefault();
    }

    private async Task NextButtonClick()
    {
        if (!_contentRenderCycle.Complete)
            return;

        var success = await FinishPageEdit();

        if (success)
            NavigateNext();
    }

    private void NavigateNext()
    {
        if (!State.ActiveStep.HasValue)
            return;

        State.ActiveStep = State.Steps.Skip(State.ActiveStep.Value.Number).FirstOrDefault();
    }

    private async Task FinishButtonClick()
    {
        if (!_contentRenderCycle.Complete)
            return;

        var success = await FinishPageEdit();

        if (success)
            await FinishWizard();
    }

    private async Task FinishWizard()
    {
        // todo: in some cases we surely need specific finish handling, so we should have something like an optional finish handler

        await ExitWizard();
    }

    private async Task ExitWizard()
    {
        if (OnExit.HasDelegate)
            await OnExit.InvokeAsync();
    }

    private async Task ExitButtonClick()
        => await ExitWizard();

    private Task PageEditServiceBeginOrCancelEdit()
    {
        State.LastSaveResult = null;

        return Task.CompletedTask;
    }

    private void BeginFinishOrCancelPageEdit()
    {
        State.BeginUpdate();
        try
        {
            State.LoadingOverlayVisible = true;
            State.LastSaveResult = null;
        }
        finally
        {
            State.EndUpdate();
        }
    }

    private async Task<bool> FinishPageEdit()
    {
        BeginFinishOrCancelPageEdit();

        State.BeginUpdate();
        try
        {
            State.LastSaveResult = await PageEditService.FinishEdit();

            return State.LastSaveResult.Success;
        }
        finally
        {
            State.LoadingOverlayVisible = false;

            State.EndUpdate();
        }
    }

    private async Task<bool> CancelPageEdit()
    {
        BeginFinishOrCancelPageEdit();

        State.BeginUpdate();
        try
        {
            await PageEditService.CancelEdit();

            State.LastSaveResult = null;

            return true;
        }
        catch (Exception e)
        {
            UnexpectedErrorOnCancelPageEdit(Logger, e);

            State.LastSaveResult = new WizardPageEditFinishResult(false,
                string.Format(CultureInfo.CurrentCulture, "{0} {1}", CommonPhrases.AnUnexpectedErrorOccurred,
                    "See logs for further details."));

            return false;
        }
        finally
        {
            // close loading spinner and refresh component to show the change
            State.LoadingOverlayVisible = false;

            State.EndUpdate();
        }
    }

    private void CancelIntermediateRequests()
        => _requestedNavigateBack = false;

    private void ExecuteIntermediateRequests()
    {
        if (_requestedNavigateBack)
        {
            NavigateBack();

            _requestedNavigateBack = false;
        }
    }

    internal void OnCancelUnsavedChangesPopup()
        => CancelIntermediateRequests();

    internal async Task OnRevertUnsavedChangesPopup()
    {
        var success = await CancelPageEdit();

        if (success)
            ExecuteIntermediateRequests();
        else
            CancelIntermediateRequests();
    }

    internal async Task OnSaveUnsavedChangesPopup()
    {
        var success = await FinishPageEdit();

        if (success)
            ExecuteIntermediateRequests();
        else
            CancelIntermediateRequests();
    }

    [LoggerMessage(1, LogLevel.Error, "Unexpected error occurred while cancel page edit")]
    private static partial void UnexpectedErrorOnCancelPageEdit(ILogger<WizardBody<TContext>> logger, Exception exception);
}
