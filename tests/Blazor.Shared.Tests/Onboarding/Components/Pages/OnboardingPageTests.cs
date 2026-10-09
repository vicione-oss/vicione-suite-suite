using Blazor.Shared.Onboarding.Components.Pages;
using Blazor.Shared.Onboarding.Models;
using Blazor.Shared.Onboarding.NavTiles;
using Blazor.Shared.Services;
using Blazor.Tests.Tools;
using Bunit;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Authorization;
using Sdk.Client.NavTiles.Enums;
using Sdk.Client.NavTiles.Services;
using Sdk.Client.Wizards.Components;
using Sdk.Client.Wizards.Services;
using Sdk.Instance;

namespace Blazor.Shared.Tests.Onboarding.Components.Pages;

public sealed class OnboardingPageTests : IAsyncDisposable
{
    private readonly BunitContext _ctx = new();
    private readonly IOnboardingStateStore _onboardingStateStore = Substitute.For<IOnboardingStateStore>();
    private readonly INavTileRegistry<SharedClientModule> _navTileRegistry = Substitute.For<INavTileRegistry<SharedClientModule>>();
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
    private readonly OnboardingState _onboardingState;

    public OnboardingPageTests()
    {
        IInstanceInformationProvider? instanceInformationProvider = null;

        _ctx.SetupBlazorUiComponents(setup => instanceInformationProvider = setup.InstanceInformationProvider);

        _onboardingState = new OnboardingState { InstanceId = instanceInformationProvider!.Local.Id };

        _onboardingStateStore
            .GetOnboardingStateAsync(_onboardingState.InstanceId, Arg.Any<CancellationToken>())
            .Returns(_onboardingState);

        // The wizard content is replaced by an empty component, so only the page's own reaction to a closing wizard is under test.
        var contentComponentTypeProvider = Substitute.For<IWizardContentComponentTypeProvider>();
        contentComponentTypeProvider.GetWizardContentComponentType<IOnboardingWizardContext>().Returns(typeof(EmptyWizardContent));

        _ctx.Services
            .AddSingleton(_onboardingStateStore)
            .AddSingleton(_navTileRegistry)
            .AddSingleton(_navigationService)
            .AddSingleton(contentComponentTypeProvider)
            .AddSingleton(Substitute.For<IOnboardingWizardContext>());
    }

    public ValueTask DisposeAsync()
        => _ctx.DisposeAsync();

    private async Task CloseWizard()
    {
        var page = _ctx.Render<OnboardingPage>();
        var wizard = page.FindComponent<Wizard<IOnboardingWizardContext>>();

        await page.InvokeAsync(() => wizard.Instance.VisibleChanged.InvokeAsync(false));
    }

    [Fact]
    public async Task Should_remove_nav_tile_when_wizard_closes_after_completion()
    {
        // Arrange
        _onboardingState.Completed = true;

        // Act
        await CloseWizard();

        // Assert
        _onboardingState.ShowWizardWhenNotCompleted.Should().BeFalse();
        await _onboardingStateStore.Received(1).SetOnboardingStateAsync(_onboardingState, Arg.Any<CancellationToken>());
        _navTileRegistry.Received(1).Remove(OnboardingNavTile.Id);
        _navigationService.Received(1).NavigateToRootPage();
    }

    [Fact]
    public async Task Should_offer_nav_tile_when_wizard_closes_before_completion()
    {
        // Arrange
        _onboardingState.Completed = false;

        // Act
        await CloseWizard();

        // Assert
        _onboardingState.ShowWizardWhenNotCompleted.Should().BeFalse();
        await _onboardingStateStore.Received(1).SetOnboardingStateAsync(_onboardingState, Arg.Any<CancellationToken>());
        _navTileRegistry.Received(1).Add<OnboardingNavTile>(OnboardingNavTile.Id,
            linkTarget: Shared.Onboarding.Constants.Route,
            group: NavTileGroup.Administration,
            authorizationRequirement: Arg.Any<AccessLevelAuthorizationRequirement>());
        _navigationService.Received(1).NavigateToRootPage();
    }

    private sealed class EmptyWizardContent : ComponentBase, IWizardContent<IOnboardingWizardContext>
    {
        [Parameter] public string Title { get; set; } = string.Empty;
        [Parameter] public IOnboardingWizardContext Context { get; set; } = default!;
        [Parameter] public bool Visible { get; set; }
        [Parameter] public EventCallback<bool> VisibleChanged { get; set; }
        [Parameter] public bool AllowExit { get; set; }
    }
}
