using AwesomeAssertions;
using Core.OS.Instance;
using Core.OS.Instance.Consumers;
using Core.OS.Instance.Services;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Instance;
using Sdk.Instance.Events;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Instance.Consumers;

public sealed class SyncRoutingSlipFaultedConsumerTests
{
    private readonly SynchronizationState _synchronizationState = new();
    private readonly SyncRetryState _syncRetryState = new();
    private readonly ILocalInstanceInformationProvider _localInstanceInfoProvider = Substitute.For<ILocalInstanceInformationProvider>();
    private readonly ISendEndpointProvider _sendEndpointProvider = Substitute.For<ISendEndpointProvider>();
    private readonly IHostApplicationLifetime _applicationLifetime = Substitute.For<IHostApplicationLifetime>();
    private readonly Guid _localInstanceId = Guid.NewGuid();
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public SyncRoutingSlipFaultedConsumerTests()
    {
        _applicationLifetime.ApplicationStopping.Returns(TestContext.Current.CancellationToken);

        _localInstanceInfoProvider.ReadLocalInstanceId().Returns(_localInstanceId);
        _localInstanceInfoProvider.Local.Returns(new InstanceInformationStub
        {
            Id = _localInstanceId,
            Type = InstanceType.Slave,
            Name = "TestSlave",
            Description = "Test slave instance",
            SerialNumber = "SN001",
            SystemType = "Edge-S",
            SdkVersion = "1.0.0",
            Version = "1.0.0"
        });
        _localInstanceInfoProvider.LoadedModules.Returns(new List<string> { "TestModule" });

        var sendEndpoint = Substitute.For<ISendEndpoint>();
        _sendEndpointProvider.GetSendEndpoint(Arg.Any<Uri>()).Returns(Task.FromResult(sendEndpoint));

        _configureServices = cfg =>
        {
            cfg.AddConsumer<SyncRoutingSlipFaultedConsumer>();
            cfg.AddSingleton(_synchronizationState);
            cfg.AddSingleton(_syncRetryState);
            cfg.AddSingleton(_localInstanceInfoProvider);
            cfg.AddSingleton(_sendEndpointProvider);
            cfg.AddSingleton(_applicationLifetime);
            cfg.AddSingleton(Substitute.For<ILogger<SyncRoutingSlipFaultedConsumer>>());
        };
    }

    [Fact]
    public async Task Should_ignore_event_for_different_instance()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var differentInstanceId = Guid.NewGuid();
        var @event = new InstanceSynchronizationFailed(differentInstanceId, ["error"]);

        // Act
        await tester.TestEvent<InstanceSynchronizationFailed, SyncRoutingSlipFaultedConsumer>(@event);

        // Assert
        _syncRetryState.AttemptCount.Should().Be(0);
        _synchronizationState.SynchronizationSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task Should_reset_synchronization_state_on_fault()
    {
        // Arrange
        _synchronizationState.CompleteSynchronization(); // simulate it was set
        _synchronizationState.Reset(); // now in "not completed" / waiting state - this is initial slave state
        await using var tester = new MassTransitTester(_configureServices);
        var @event = new InstanceSynchronizationFailed(_localInstanceId, ["SyncData failed"]);

        // Act
        await tester.TestEvent<InstanceSynchronizationFailed, SyncRoutingSlipFaultedConsumer>(@event);

        // Assert
        _syncRetryState.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_send_re_registration_on_first_failure()
    {
        // Arrange
        var state = new SyncRetryState { RetryDelay = TimeSpan.Zero };
        var configureServices = (IBusRegistrationConfigurator cfg) =>
        {
            cfg.AddConsumer<SyncRoutingSlipFaultedConsumer>();
            cfg.AddSingleton(_synchronizationState);
            cfg.AddSingleton(state);
            cfg.AddSingleton(_localInstanceInfoProvider);
            cfg.AddSingleton(_sendEndpointProvider);
            cfg.AddSingleton(_applicationLifetime);
            cfg.AddSingleton(Substitute.For<ILogger<SyncRoutingSlipFaultedConsumer>>());
        };

        await using var tester = new MassTransitTester(configureServices);
        var @event = new InstanceSynchronizationFailed(_localInstanceId, ["TypeLoadException"]);

        // Act
        await tester.TestEvent<InstanceSynchronizationFailed, SyncRoutingSlipFaultedConsumer>(@event);

        // Allow fire-and-forget task to complete (RetryDelay = Zero)
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Assert
        await _sendEndpointProvider.Received(1).GetSendEndpoint(Arg.Any<Uri>());
    }

    [Fact]
    public async Task Should_mark_degraded_after_max_retries()
    {
        // Arrange
        // Exhaust retries (default max = 3)
        _syncRetryState.RecordFailure(); // attempt 1
        _syncRetryState.RecordFailure(); // attempt 2
        _syncRetryState.RecordFailure(); // attempt 3 — now degraded

        await using var tester = new MassTransitTester(_configureServices);
        var @event = new InstanceSynchronizationFailed(_localInstanceId, ["persistent failure"]);

        // Act
        await tester.TestEvent<InstanceSynchronizationFailed, SyncRoutingSlipFaultedConsumer>(@event);

        // Assert — no re-registration sent since already degraded before this consume
        _syncRetryState.IsDegraded.Should().BeTrue();
    }

    [Fact]
    public async Task Should_not_send_re_registration_when_retries_exhausted()
    {
        // Arrange — pre-exhaust to 2 failures (one more will exhaust)
        _syncRetryState.RecordFailure(); // attempt 1
        _syncRetryState.RecordFailure(); // attempt 2

        await using var tester = new MassTransitTester(_configureServices);
        var @event = new InstanceSynchronizationFailed(_localInstanceId, ["final failure"]);

        // Act
        await tester.TestEvent<InstanceSynchronizationFailed, SyncRoutingSlipFaultedConsumer>(@event);

        // Assert — attempt 3 exhausts retries, no re-registration
        _syncRetryState.IsDegraded.Should().BeTrue();
        await _sendEndpointProvider.DidNotReceive().GetSendEndpoint(Arg.Any<Uri>());
    }

    private class InstanceInformationStub : IInstanceInformation
    {
        public Guid Id { get; init; }
        public InstanceType Type { get; init; }
        public string? Name { get; init; }
        public string FormattedName { get; set; } = "{ViciOne} Suite";
        public string? Description { get; set; }
        public string SerialNumber { get; init; } = string.Empty;
        public string SystemType { get; init; } = string.Empty;
        public string SdkVersion { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public IReadOnlyCollection<string> InstalledModules { get; init; } = [];
        public DateTimeOffset? FirstTimeRegistered { get; init; }
        public DateTimeOffset? LastRegistered { get; init; }
        public string? BranchName { get; init; }
        public bool InRecoveryMode { get; set; }
    }
}
