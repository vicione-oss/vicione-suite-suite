using System.Reflection;
using Blazor.Shared.Authorization.Extensions;
using Blazor.Shared.Connections.Services;
using Blazor.Shared.Services;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Logging;
using Sdk.Authorization;
using Sdk.Client.Services;
using Sdk.Instance;

namespace Blazor.Shared.Components;

public sealed partial class Routes : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly List<string> _moduleStylesheets = [];
    private readonly List<Assembly> _additionalAssemblies = [];

    private bool _initialized;
    private bool _disposed;

    [Inject] private IClientModuleService ModuleService { get; set; } = default!;
    [Inject] private IServiceProvider ServiceProvider { get; set; } = default!;
    [Inject] private INavigationService NavigationService { get; set; } = default!;
    [Inject] private ISuiteConnectionService ConnectionService { get; set; } = default!;
    [Inject] private ILogger<Routes> Logger { get; set; } = default!;
    [Inject] private IOnboardingStateStore OnboardingStateStore { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private IEnumerable<IAuthorizationHandler> AuthorizationHandlers { get; set; } = default!;
    [Inject] private IInstanceInformationProvider InstanceInformationProvider { get; set; } = default!;

    protected override void OnInitialized()
    {
        // load main assembly to ensure login page and stuff is reachable
        _additionalAssemblies.Add(NavigationService.GetType().Assembly);

        _additionalAssemblies.AddRange(ModuleService.GetModuleAssemblies());
        _moduleStylesheets.AddRange(ModuleService.GetAllModuleStylesheets());
    }

    protected override async Task OnInitializedAsync()
    {
        if (_disposed)
            return;

        // here the service provider is injected scoped so we initialize our shared scope
        await ModuleService.InitializeServices(ServiceProvider);

        await ConnectionService.Initialize(_cancellationTokenSource.Token);

        _initialized = true;
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();

        _disposed = true;
    }

    private async Task OnNavigateAsync(NavigationContext args)
    {
        try
        {
            if (args.Path != Onboarding.Constants.NavigationPath)
            {
                // navigation to non-onboarding URL

                var user = await AuthenticationStateProvider.GetUser();
                if (user is null)
                    return;

                var accessLevelAuthorizationRequirement = new AccessLevelAuthorizationRequirement(SharedClientModule.ModuleId, AccessLevel.Full);

                var authorizationHandlerContext = new AuthorizationHandlerContext([accessLevelAuthorizationRequirement], user, resource: null);

                var authorizationHandleTasks = AuthorizationHandlers.Select(h => h.HandleAsync(authorizationHandlerContext));

                await Task.WhenAll(authorizationHandleTasks).WaitAsync(_cancellationTokenSource.Token);

                if (!authorizationHandlerContext.HasSucceeded)
                    return;

                var onboardingState = await GetOnboardingState();

                if (!onboardingState.Completed && onboardingState.ShowWizardWhenNotCompleted)
                    NavigationService.NavManager.NavigateTo(Onboarding.Constants.Route, forceLoad: true);
            }
            else
            {
                // navigation to onboarding URL

                var onboardingState = await GetOnboardingState();

                if (onboardingState.Completed)
                    NavigationService.NavigateToRootPage();
            }
        }
        catch (OperationCanceledException)
        {
            // nothing to do here, we just return gracefully
        }
        catch (Exception ex)
        {
            Logger.LogError("Error: {Message}", ex.Message);
        }
    }

    private async Task<IOnboardingState> GetOnboardingState()
    {
        var instanceId = InstanceInformationProvider.Local.Id;

        var onboardingState = await OnboardingStateStore.GetOnboardingStateAsync(instanceId, _cancellationTokenSource.Token);

        return onboardingState;
    }
}
