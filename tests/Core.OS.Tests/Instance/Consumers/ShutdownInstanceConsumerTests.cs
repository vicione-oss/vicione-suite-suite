using Core.OS.Instance;
using Core.OS.Instance.Consumers;
using Core.OS.Tests.Extensions;
using Core.Shared.Instance.Contracts;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Instance.Consumers;

public class ShutdownInstanceConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IHostApplicationLifetime _applicationLifetime = Substitute.For<IHostApplicationLifetime>();
    private readonly ILocalInstanceInformationProvider _informationProvider = Substitute.For<ILocalInstanceInformationProvider>();
    private readonly Guid _instanceId = Guid.NewGuid();

    public ShutdownInstanceConsumerTests()
    {
        _configureServices = cfg =>
        {
            cfg.AddConsumer<ShutdownInstanceConsumer>();
            cfg.AddSingleton(_applicationLifetime);
            cfg.AddSingleton(_informationProvider);
        };
    }

    [Fact]
    public async Task Should_trigger_application_shutdown()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        _informationProvider.SetupLocalInstanceInformation(guid: _instanceId);
        var command = new ShutdownInstance()
        {
            InstanceId = _instanceId,
            Reason = "Modules changed",
            Delay = TimeSpan.FromMilliseconds(100),
        };

        // Act
        await tester.TestInstanceDependentCommand<ShutdownInstance, ShutdownInstanceConsumer>(command);

        // Assert
        _applicationLifetime.Received(1).StopApplication();
    }

    [Fact]
    public async Task Should_skip_application_shutdown_with_wrong_instance_id()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        _informationProvider.SetupLocalInstanceInformation(guid: _instanceId);
        var command = new ShutdownInstance()
        {
            InstanceId = Guid.NewGuid(),
            Reason = "Modules changed",
            Delay = TimeSpan.FromMilliseconds(100),
        };

        // Act
        await tester.TestInstanceDependentCommand<ShutdownInstance, ShutdownInstanceConsumer>(command);

        // Assert
        _applicationLifetime.Received(0).StopApplication();
    }
}
