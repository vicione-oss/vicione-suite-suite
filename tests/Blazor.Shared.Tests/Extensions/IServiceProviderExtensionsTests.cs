using Blazor.Shared.Connections.Services;
using Blazor.Shared.Extensions;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.DateAndTime.Services;
using Core.Shared.HostManagement.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Blazor.Shared.Tests.Extensions;

public sealed class IServiceProviderExtensionsTests
{
    public sealed class InitializeSharedServices
    {
        [Fact]
        public async Task Should_initialize_shared_services()
        {
            // Arrange
            using var services = new ServiceCollection()
                .AddLogging()
                .AddSingleton(Substitute.For<ITimeZoneDescriptorProvider>())
                .AddSingleton(Substitute.For<IClientTimeProvider>())
                .AddSingleton(Substitute.For<ISuiteConnectionService>())
                .AddSingleton(Substitute.For<ISystemConfigurationService>())
                .BuildServiceProvider();

            // Act
            await services.InitializeSharedServices(TestContext.Current.CancellationToken);

            // Assert
            await services.GetRequiredService<ITimeZoneDescriptorProvider>().Received(1).GetAll(Arg.Any<CancellationToken>());
            await services.GetRequiredService<IClientTimeProvider>().Received(1).Initialize(Arg.Any<CancellationToken>());
            await services.GetRequiredService<ISuiteConnectionService>().Received(1).Initialize(Arg.Any<CancellationToken>());
            await services.GetRequiredService<ISystemConfigurationService>().Received(1).Initialize(Arg.Any<CancellationToken>());
        }
    }
}
