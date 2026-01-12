using Blazor.Shared.Onboarding.Extensions;
using Blazor.Shared.Onboarding.Models;
using Blazor.Shared.Services;
using Core.Shared.Instance.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Authorization;
using Sdk.Client.NavTiles.Services;
using Sdk.Instance;

namespace Blazor.Shared.Onboarding.Components.Pages;

[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class OnboardingPage : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private bool _wizardVisible = true;

    [Inject] private IOnboardingWizardContext OnboardingWizardContext { get; set; } = default!;
    [Inject] private IOnboardingStateStore OnboardingStateStore { get; set; } = default!;
    [Inject] private INavigationService NavigationService { get; set; } = default!;
    [Inject] private INavTileRegistry<SharedClientModule> NavTileRegistry { get; set; } = default!;
    [Inject] private IInstanceInformationProvider InstanceInformationProvider { get; set; } = default!;

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }

    private async Task WizardClosed()
    {
        try
        {
            var instanceId = InstanceInformationProvider.Local.Id;

            var onboardingState = await OnboardingStateStore.GetOnboardingStateAsync(instanceId, _cancellationTokenSource.Token);
            onboardingState.ShowWizardWhenNotCompleted = false;
            await OnboardingStateStore.SetOnboardingStateAsync(onboardingState, _cancellationTokenSource.Token);

            if (onboardingState.Completed)
                NavTileRegistry.RemoveOnboardingNavTile();
            else
                NavTileRegistry.AddOnboardingNavTile();

            NavigationService.NavigateToRootPage();
        }
        catch (OperationCanceledException)
        {
            // Nothing to do here, return gracefully
        }
        catch (ObjectDisposedException)
        {
            // CancellationTokenSource already disposed, return gracefully
        }
    }
}
