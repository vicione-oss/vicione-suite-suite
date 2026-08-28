using AwesomeAssertions;
using Core.OS.EnvironmentOverrides;
using Core.OS.EnvironmentOverrides.Consumers;
using Core.Shared.EnvironmentOverrides.Commands;
using Core.Shared.EnvironmentOverrides.Events;
using Core.Shared.HostManagement.Events;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.EnvironmentOverrides.Consumers;

public sealed class SetEnvironmentOverridesConsumerTests
{
    private readonly IEnvironmentOverridesRepository _repository = Substitute.For<IEnvironmentOverridesRepository>();
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public SetEnvironmentOverridesConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<SetEnvironmentOverridesConsumer>();
            cfg.AddSingleton(_repository);
        };

    [Fact]
    public async Task Should_store_overrides_and_publish_changed_and_restart_required()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);

        var overrides = new Dictionary<string, string> { ["KEY"] = "value" };
        var command = new SetEnvironmentOverrides(overrides);

        // Act
        await tester.TestInstanceDependentCommand<SetEnvironmentOverrides, SetEnvironmentOverridesConsumer>(command);

        // Assert
        await _repository.Received(1).Store(
            Arg.Is<IReadOnlyDictionary<string, string>>(o => o != null && o.Count == overrides.Count
                && !o.Except(overrides).Any()),
            Arg.Any<CancellationToken>());

        (await tester.Harness.Published.Any<EnvironmentOverridesChanged>(TestContext.Current.CancellationToken)).Should().BeTrue();

        SystemRestartRequired? restartRequired = null;
        await foreach (var published in tester.Harness.Published.SelectAsync<SystemRestartRequired>(TestContext.Current.CancellationToken))
        {
            restartRequired = published.Context.Message;
            break;
        }
        restartRequired.Should().NotBeNull();
        restartRequired!.Reason.Should().Be(RestartReason.EnvironmentConfiguration);
        restartRequired.CorrelationId.Should().Be(command.CorrelationId);
    }

    [Fact]
    public async Task Should_publish_error_event_on_exception()
    {
        // Arrange
        _repository.Store(Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("test failure"));

        await using var tester = new MassTransitTester(_configureServices);

        var command = new SetEnvironmentOverrides(new Dictionary<string, string> { ["KEY"] = "value" });

        // Act
        await tester.TestInstanceDependentCommand<SetEnvironmentOverrides, SetEnvironmentOverridesConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<SetEnvironmentOverridesError>(TestContext.Current.CancellationToken)).Should().BeTrue();
        (await tester.Harness.Published.Any<EnvironmentOverridesChanged>(TestContext.Current.CancellationToken)).Should().BeFalse();
        (await tester.Harness.Published.Any<SystemRestartRequired>(TestContext.Current.CancellationToken)).Should().BeFalse();
    }
}
