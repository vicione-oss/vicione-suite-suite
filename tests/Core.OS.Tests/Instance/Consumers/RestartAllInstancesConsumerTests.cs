using Core.OS.DbContext;
using Core.OS.Instance.Consumers;
using Core.OS.Tests.Extensions;
using Core.Shared.Instance.Commands;
using AwesomeAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Instance.Consumers;

public sealed class RestartAllInstancesConsumerTests : TestWithDbContextSqlite<ApplicationDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public RestartAllInstancesConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<RestartAllInstancesConsumer>();
            cfg.AddSingleton<IApplicationDbContext>(_ => TestDbContext);
        };

    [Fact]
    public async Task Should_consume_command()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new RestartAllInstances();

        // Act
        await tester.TestCommand<RestartAllInstances, RestartAllInstancesConsumer>(command);

        // Assert
        var consumed = await tester.Harness.Consumed.Any<RestartAllInstances>(
            k => k.Context.Message.CorrelationId == command.CorrelationId, TestContext.Current.CancellationToken);
        consumed.Should().BeTrue();
    }

    [Fact]
    public async Task Should_send_restart_to_every_instance()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var instances = tester.Services.GetRequiredService<IApplicationDbContext>().SeedInstanceInfos(3).ToList();
        var command = new RestartAllInstances { Delay = TimeSpan.FromSeconds(10) };

        // Act
        await tester.TestCommand<RestartAllInstances, RestartAllInstancesConsumer>(command);

        // Assert: every registered instance receives a restart command with the propagated delay and correlation
        foreach (var instance in instances)
        {
            var sent = await tester.Harness.Sent.Any<ControlInstance>(
                k => k.Context.Message.InstanceId == instance.Id
                    && k.Context.Message.Action == InstanceCommand.Restart
                    && k.Context.Message.Delay == command.Delay
                    && k.Context.Message.CorrelationId == command.CorrelationId,
                TestContext.Current.CancellationToken);
            sent.Should().BeTrue();
        }
    }

    [Fact]
    public async Task Should_not_send_when_no_instances_registered()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new RestartAllInstances();

        // Act
        await tester.TestCommand<RestartAllInstances, RestartAllInstancesConsumer>(command);

        // Assert
        var sent = await tester.Harness.Sent.Any<ControlInstance>(TestContext.Current.CancellationToken);
        sent.Should().BeFalse();
    }
}
