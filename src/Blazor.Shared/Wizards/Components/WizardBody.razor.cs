using System.Globalization;
using Blazor.Shared.Popup.Services;
using Blazor.Shared.Settings.Models;
using Blazor.Shared.Wizards.Extensions;
using Blazor.Shared.Wizards.Factories;
using Blazor.Shared.Wizards.Models;
using Blazor.Shared.Wizards.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Sdk.Client.Models;
using Sdk.Client.Services;
using Sdk.Client.Wizards.Models;
using Sdk.Client.Wizards.Services;

namespace Blazor.Shared.Wizards.Components;

public sealed partial class WizardBody<TContext> : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly SemaphoreSlim _updateStepsSemaphore = new(1);
    private readonly SemaphoreSlim _stepsUpdatedSemaphore = new(1);

    private bool _initialized;
    private bool _disposed;

    private bool _requestedNavigateBack;
    private bool _requestedExit;

    private WizardBodyContentRenderCycle _contentRenderCycle = new();
    private int _wizardPageEditRunningCount;
    private readonly Lock _wizardPageEditRunningCountLock = new();

    [Inject] private ILogger<WizardBody<TContext>> Logger { get; set; } = default!;
    [Inject] private IWizardState State { get; set; } = default!;
    [Inject] private IWizardPageRegistry<TContext> PageRegistry { get; set; } = default!;
    [Inject] private IWizardStepFactory StepFactory { get; set; } = default!;
    [Inject] private ILoadingIndicationPlacementBehavior LoadingIndicationPlacementBehavior { get; set; } = default!;
    [Inject] private IWizardPageEditRegistry WizardPageEditRegistry { get; set; } = default!;

    [Parameter, EditorRequired] public string Title { get; set; }
    [Parameter] public bool AllowExit { get; set; }
    [Parameter] public EventCallback OnExit { get; set; }
    [Parameter] public EventCallback OnShowUnsavedChangesPopup { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (Interlocked.CompareExchange(ref _initialized, true, false))
            return;

        WizardPageEditRegistry.Changed += WizardPageEditRegistryChanged;

        foreach (var wizardPageEdit in WizardPageEditRegistry)
            wizardPageEdit.Changed += WizardPageEditChanged;

        State.Changed += StateChanged;

        await UpdateSteps(resetActiveStep: true);

        PageRegistry.Changed += PageRegistryChanged;

        LoadingIndicationPlacementBehavior.PlacementChanged += LoadingIndicationPlacementChanged;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        LoadingIndicationPlacementBehavior.PlacementChanged -= LoadingIndicationPlacementChanged;

        PageRegistry.Changed -= PageRegistryChanged;

        State.Changed -= StateChanged;

        WizardPageEditRegistry.Changed -= WizardPageEditRegistryChanged;

        foreach (var wizardPageEdit in WizardPageEditRegistry)
            wizardPageEdit.Changed -= WizardPageEditChanged;

        _contentRenderCycle.Completed -= ContentRenderCycleCompleted;

        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();

        _updateStepsSemaphore.Dispose();
        _stepsUpdatedSemaphore.Dispose();

        _disposed = true;
    }

    private void WizardPageEditRegistryChanged(RegistryChangedEventArgs<IWizardPageEdit> args)
    {
        foreach (var wizardPageEdit in args.ItemsRemoved)
            wizardPageEdit.Changed -= WizardPageEditChanged;

        foreach (var wizardPageEdit in args.ItemsAdded)
            wizardPageEdit.Changed += WizardPageEditChanged;
    }

    private void WizardPageEditChanged(PropertiesChangedEventArgs args)
    {
        if (args.Sender is IWizardPageEdit wizardPageEdit && args.PropertyNames.Contains(nameof(IControlPanelEdit.IsRunning)))
        {
            lock (_wizardPageEditRunningCountLock)
            {
                if (wizardPageEdit.IsRunning)
                    _wizardPageEditRunningCount += 1;
                else
                    _wizardPageEditRunningCount -= 1;

                if (_wizardPageEditRunningCount < 0)
                    _wizardPageEditRunningCount = 0;
            }

            UpdateContentActionButtonStates();
        }
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
        var activeStepChanged = args.PropertyNames.Contains(nameof(State.ActiveStep));

        if (activeStepChanged || args.PropertyNames.Contains(nameof(State.Steps)))
        {
            try
            {
                var cancellationToken = _cancellationTokenSource.Token;

                await _stepsUpdatedSemaphore.WaitAsync(cancellationToken);

                try
                {
                    // Here we are in a situation where step states have already been changed,
                    // which means UI elements like stepper or the content area will most likely
                    // render different elements compared to before, so we handle this statewise
                    // as start from scratch.

                    if (activeStepChanged)
                    {
                        // Cancel possibly ongoing edits when active step has changed
                        await CancelWizardPageEdits();
                    }

                    _contentRenderCycle.Completed -= ContentRenderCycleCompleted;
                    _contentRenderCycle = new(State.ActiveStep.HasValue ? [State.ActiveStep.Value.WizardPageRegistryItem] : []);
                    _contentRenderCycle.Completed += ContentRenderCycleCompleted;

                    State.BeginUpdate();
                    try
                    {
                        UpdateContentActionButtonStates();

                        State.LastSaveErrorResults = [];
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
            args.PropertyNames.Contains(nameof(State.LastSaveErrorResults)))
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

        if (IsAnyWizardPanelEditRunning())
        {
            _requestedNavigateBack = true;

            await OnShowUnsavedChangesPopup.InvokeAsync();
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

        var success = await SaveWizardPageEdits();

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

        var success = await SaveWizardPageEdits();

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
    {
        if (!_contentRenderCycle.Complete)
            return;

        if (IsAnyWizardPanelEditRunning())
        {
            _requestedExit = true;

            await OnShowUnsavedChangesPopup.InvokeAsync();
        }
        else
        {
            await ExitWizard();
        }
    }

    private void BeginFinishOrCancelPageEdit()
    {
        State.BeginUpdate();
        try
        {
            State.LoadingOverlayVisible = true;
            State.LastSaveErrorResults = [];
        }
        finally
        {
            State.EndUpdate();
        }
    }

    private async Task<bool> SaveWizardPageEdits()
    {
        Logger.LogDebug("SaveWizardPageEdits called");

        BeginFinishOrCancelPageEdit();

        State.BeginUpdate();
        try
        {
            State.LastSaveErrorResults = [];

            var wizardPageEdits = WizardPageEditRegistry.ToArray();

            var saveTasks = wizardPageEdits

                // No check for _isRunning as we need to execute save also for steps
                // that represent a finish step AND nothing is editable in these steps
                //.Where(wizardPageEdit => wizardPageEdit.IsRunning)

                .Select(wizardPageEdit => wizardPageEdit.Save())
                .ToArray();

            var saveResults = await Task.WhenAll(saveTasks);

            State.LastSaveErrorResults = [.. saveResults.OfType<SaveErrorResult>()];

            var success = State.LastSaveErrorResults.Count == 0;

            return success;
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Unexpected error occurred while saving edits");

            State.LastSaveErrorResults = [ new SaveErrorResult(string.Format(CultureInfo.CurrentCulture,
                Localization.WizardBody.UnexpectedErrorWhileSavingEdits, e.Message)) ];

            return false;
        }
        finally
        {
            State.LoadingOverlayVisible = false;

            State.EndUpdate();
        }
    }

    private async Task<bool> CancelWizardPageEdits()
    {
        Logger.LogDebug("CancelWizardPageEdits called");

        BeginFinishOrCancelPageEdit();

        State.BeginUpdate();
        try
        {
            State.LastSaveErrorResults = [];

            var wizardPageEdits = WizardPageEditRegistry.ToArray();
            var cancelTasks = wizardPageEdits.Select(wizardPageEdit => wizardPageEdit.Cancel(withReset: true)).ToArray();

            var cancelResults = await Task.WhenAll(cancelTasks);

            var success = cancelResults.All(cancelResult => cancelResult);

            return success;
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Unexpected error occurred while cancelling edits");

            State.LastSaveErrorResults = [ new SaveErrorResult(string.Format(CultureInfo.CurrentCulture,
                Localization.WizardBody.UnexpectedErrorWhileCancellingEdits, e.Message)) ];

            return false;
        }
        finally
        {
            State.LoadingOverlayVisible = false;

            State.EndUpdate();
        }
    }

    private void CancelIntermediateRequests()
    {
        _requestedNavigateBack = false;
        _requestedExit = false;
    }

    private async Task ExecuteIntermediateRequests()
    {
        if (_requestedNavigateBack)
        {
            _requestedNavigateBack = false;

            NavigateBack();
        }

        if (_requestedExit)
        {
            _requestedExit = false;

            await ExitWizard();
        }
    }

    internal void OnCancelUnsavedChangesPopup()
        => CancelIntermediateRequests();

    internal async Task OnRevertUnsavedChangesPopup()
    {
        var success = await CancelWizardPageEdits();

        if (success)
            await ExecuteIntermediateRequests();
        else
            CancelIntermediateRequests();
    }

    internal async Task OnSaveUnsavedChangesPopup()
    {
        var success = await SaveWizardPageEdits();

        if (success)
            await ExecuteIntermediateRequests();
        else
            CancelIntermediateRequests();
    }

    private bool IsAnyWizardPanelEditRunning()
    {
        lock (_wizardPageEditRunningCountLock)
            return _wizardPageEditRunningCount > 0;
    }

    [LoggerMessage(1, LogLevel.Error, "Unexpected error occurred while cancel page edit")]
    private static partial void UnexpectedErrorOnCancelPageEdit(ILogger<WizardBody<TContext>> logger, Exception exception);
}
