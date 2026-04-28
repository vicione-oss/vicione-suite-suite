using Blazor.Shared.Onboarding.Extensions;
using Blazor.Shared.Onboarding.NavTiles;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Authorization;
using Sdk.Client.NavTiles.Enums;
using Sdk.Client.NavTiles.Services;
using Sdk.Instance;
using Xunit;

namespace Blazor.Shared.Tests.Onboarding.Extensions;

public class IServiceProviderExtensionsTests
{
    public sealed class UseOnboarding : IServiceProviderExtensionsTests
    {
        private readonly InstanceInformation _instanceInformation = new() { Id = Guid.NewGuid(), Type = InstanceType.Standalone };
        private readonly IInstanceInformationProvider _informationProvider = Substitute.For<IInstanceInformationProvider>();
        private readonly IOnboardingStateStore _onboardingStateStore = Substitute.For<IOnboardingStateStore>();
        private readonly IOnboardingState _onboardingState = Substitute.For<IOnboardingState>();
        private readonly INavTileRegistry<SharedClientModule> _navTileRegistry = Substitute.For<INavTileRegistry<SharedClientModule>>();

        private ServiceProvider SetupServices()
        {
            _informationProvider.Local.Returns(_instanceInformation);
            _onboardingStateStore.GetOnboardingState(_instanceInformation.Id)
                .Returns(_onboardingState);

            return new ServiceCollection()
                .AddSingleton(_informationProvider)
                .AddSingleton(_onboardingStateStore)
                .AddSingleton(_navTileRegistry)
                .BuildServiceProvider();
        }

        [Fact]
        public async Task Should_add_onboarding_nav_tile_if_not_completed_and_show_flag_is_not_set()
        {
            // Arrange            
            using var services = SetupServices();
            _onboardingState.Completed.Returns(false);
            _onboardingState.ShowWizardWhenNotCompleted.Returns(false);

            // Act
            services.UseOnboarding();

            // Assert
            _navTileRegistry.Received(1).Add<OnboardingNavTile>(OnboardingNavTile.Id,
                linkTarget: Shared.Onboarding.Constants.Route,
                group: NavTileGroup.Administration,
                authorizationRequirement: Arg.Any<AccessLevelAuthorizationRequirement>());
        }

        [Fact]
        public async Task Should_not_add_onboarding_nav_tile_if_not_completed_and_show_flag_is_set()
        {
            // Arrange            
            using var services = SetupServices();
            _onboardingState.Completed.Returns(false);
            _onboardingState.ShowWizardWhenNotCompleted.Returns(true);

            // Act
            services.UseOnboarding();

            // Assert
            _navTileRegistry.Received(0).Add<OnboardingNavTile>(OnboardingNavTile.Id,
                linkTarget: Shared.Onboarding.Constants.Route,
                group: NavTileGroup.Administration,
                authorizationRequirement: Arg.Any<AccessLevelAuthorizationRequirement>());
        }

        [Fact]
        public async Task Should_not_add_onboarding_nav_tile_if_completed()
        {
            // Arrange            
            using var services = SetupServices();
            _onboardingState.Completed.Returns(true);

            // Act
            services.UseOnboarding();

            // Assert
            _navTileRegistry.Received(0).Add<OnboardingNavTile>(OnboardingNavTile.Id,
                linkTarget: Shared.Onboarding.Constants.Route,
                group: NavTileGroup.Administration,
                authorizationRequirement: Arg.Any<AccessLevelAuthorizationRequirement>());
        }
    }
}
