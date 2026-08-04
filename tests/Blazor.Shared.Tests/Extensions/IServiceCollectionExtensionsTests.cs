using Blazor.Shared.Extensions;
using Blazor.Shared.Module.Services;
using Blazor.Shared.Settings.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Client.NotificationArea.Services;
using Sdk.Client.Services;
using Sdk.Instance;

namespace Blazor.Shared.Tests.Extensions;

public sealed class IServiceCollectionExtensionsTests
{
    public sealed class AddBlazorShared
    {
        [Fact]
        public void Should_provide_implementations_for_sdk_services()
        {
            // Arrange + Act
            using var services = new ServiceCollection()
                .AddLogging()
                .AddSingleton(Substitute.For<IJSRuntime>())
                .AddSingleton(Substitute.For<IInstanceInformationProvider>())
                .AddSingleton(Substitute.For<IUiMediator>())
                .AddSingleton(Substitute.For<IClientModuleService>())
                .AddBlazorShared()
                .BuildServiceProvider();

            // Assert
            services.GetRequiredService<IActiveNotificationElementPolicy>().Should().NotBeNull();
            services.GetRequiredService<IAuthorizationPolicyProvider>().Should().NotBeNull();
            services.GetRequiredService<ILayoutService>().Should().NotBeNull();
            services.GetRequiredService<IConnectionService>().Should().NotBeNull();
            services.GetRequiredService<IJsInterop>().Should().NotBeNull();
            services.GetRequiredService<IMessageBannerService>().Should().NotBeNull();

            services.GetRequiredService<IActiveControlPanelPageProvider>().Should().NotBeNull();
            services.GetRequiredService<IActiveControlPanelDescriptorProvider>().Should().NotBeNull();
            services.GetRequiredService<IControlPanelRequest>().Should().NotBeNull();
            services.GetRequiredService<INavigateBackRequest>().Should().NotBeNull();
        }
    }
}
