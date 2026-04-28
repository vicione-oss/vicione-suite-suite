using Blazor.Shared.Instance.ControlPanels.Instances;
using Blazor.Shared.Instance.ControlPanels.Instances.Services;
using Blazor.Shared.Instance.Extensions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Client.Services;
using Sdk.Instance;
using Xunit;

namespace Blazor.Shared.Tests.Instance.Extensions;

public class IServiceProviderExtensionsTests
{
    private static (IServiceProvider services, IControlPanelRegistry<SharedClientModule> registry) BuildServices(
        IInstanceInformationProvider informationProvider, IMessageBannerService? bannerService = null)
    {
        var registry = Substitute.For<IControlPanelRegistry<SharedClientModule>>();

        var services = new ServiceCollection()
            .AddSingleton(informationProvider)
            .AddSingleton(bannerService ?? Substitute.For<IMessageBannerService>())
            .AddSingleton(Substitute.For<IUiMediator>())
            .AddSingleton(registry)
            .BuildServiceProvider();

        return (services, registry);
    }

    private static IInstanceInformationProvider CreateProvider(InstanceType type, bool inRecoveryMode = false)
    {
        var info = Substitute.For<IInstanceInformation>();
        info.Id.Returns(Guid.NewGuid());
        info.Type.Returns(type);
        info.InRecoveryMode.Returns(inRecoveryMode);

        var provider = Substitute.For<IInstanceInformationProvider>();
        provider.Local.Returns(info);
        return provider;
    }

    public sealed class UseInstanceManagement : IServiceProviderExtensionsTests
    {
        [Fact]
        public void Should_register_instance_control_panel_for_standalone()
        {
            // Arrange
            var (services, registry) = BuildServices(CreateProvider(InstanceType.Standalone));

            // Act
            services.UseInstanceManagement();

            // Assert
            registry.Received().Add<InstanceControlPanel, InstanceControlPanelState>(
                Arg.Any<IControlPanelDescriptor>(),
                Arg.Any<InstanceControlPanelState>(),
                Arg.Any<IControlPanelCategoryDescriptor>(),
                Arg.Any<IControlPanelGroupDescriptor?>(),
                Arg.Any<Microsoft.AspNetCore.Authorization.IAuthorizationRequirement?>());
        }

        [Fact]
        public void Should_register_instances_control_panel_for_non_standalone()
        {
            // Arrange
            var (services, registry) = BuildServices(CreateProvider(InstanceType.Slave));

            // Act
            services.UseInstanceManagement();

            // Assert
            registry.Received().Add<InstancesControlPanel, InstancesControlPanelState>(
                Arg.Any<IControlPanelDescriptor>(),
                Arg.Any<InstancesControlPanelState>(),
                Arg.Any<IControlPanelCategoryDescriptor>(),
                Arg.Any<IControlPanelGroupDescriptor?>(),
                Arg.Any<Microsoft.AspNetCore.Authorization.IAuthorizationRequirement?>());
        }

        [Fact]
        public void Should_register_instance_control_panel_for_non_standalone()
        {
            // Arrange
            var (services, registry) = BuildServices(CreateProvider(InstanceType.Slave));

            // Act
            services.UseInstanceManagement();

            // Assert
            registry.Received().Add<InstanceControlPanel, InstanceControlPanelState>(
                Arg.Any<IControlPanelDescriptor>(),
                Arg.Any<InstanceControlPanelState>(),
                Arg.Any<IControlPanelCategoryDescriptor>(),
                Arg.Any<IControlPanelGroupDescriptor?>(),
                Arg.Any<Microsoft.AspNetCore.Authorization.IAuthorizationRequirement?>());
        }

        [Fact]
        public void Should_show_recovery_mode_banner_when_in_recovery_mode()
        {
            // Arrange
            var bannerService = Substitute.For<IMessageBannerService>();
            var (services, _) = BuildServices(CreateProvider(InstanceType.Standalone, inRecoveryMode: true), bannerService);

            // Act
            services.UseInstanceManagement();

            // Assert
            bannerService.Received(1).ShowMessageBanner(
                Sdk.MessageBanner.Contracts.MessageType.Error,
                Arg.Any<string>());
        }

        [Fact]
        public void Should_not_show_recovery_mode_banner_when_not_in_recovery_mode()
        {
            // Arrange
            var bannerService = Substitute.For<IMessageBannerService>();
            var (services, _) = BuildServices(CreateProvider(InstanceType.Standalone, inRecoveryMode: false), bannerService);

            // Act
            services.UseInstanceManagement();

            // Assert
            bannerService.DidNotReceive().ShowMessageBanner(
                Arg.Any<Sdk.MessageBanner.Contracts.MessageType>(),
                Arg.Any<string>());
        }
    }
}
