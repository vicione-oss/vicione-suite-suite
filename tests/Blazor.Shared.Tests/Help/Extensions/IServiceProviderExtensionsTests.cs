using Blazor.Shared.Help.Extensions;
using Blazor.Shared.Help.NotificationArea;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.NotificationArea.Services;

namespace Blazor.Shared.Tests.Help.Extensions;

public class IServiceProviderExtensionsTests
{
    public sealed class UseHelp : IServiceProviderExtensionsTests
    {
        [Fact]
        public async Task Should_add_help_notification_element_only_on_debug()
        {
            // Arrange
            var registry = Substitute.For<INotificationElementRegistry<SharedClientModule>>();

            using var services = new ServiceCollection()
                .AddSingleton(registry)
                .BuildServiceProvider();

            // Act
            services.UseHelp();

            // Assert
#if DEBUG
            var expectedCalls = 1;
#else
            var expectedCalls = 0;
#endif
            registry.Received(expectedCalls).Add<HelpNotificationElement, NotificationElementState>(
                Arg.Any<NotificationElementState>(),
                position: 1);
        }
    }
}
