using Core.Module;
using Core.OS.MessageBus.MassTransit.Configuration;
using Core.OS.Modules;
using Core.OS.Tests.Extensions;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sdk.Instance;
using Sdk.Messaging;
using Sdk.Modules;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.MessageBus.MassTransit;

/// <summary>
/// Verifies the receive pipeline of ADR-004 end to end on a real DI graph: a consumer that throws is retried - on the
/// in-memory transport too - what a failed attempt published is discarded, and a persistently failing endpoint is
/// stopped by the kill switch.
/// </summary>
public class MessageRetryPipelineTests
{
    /// <summary>Activation threshold used by the kill switch test.</summary>
    private const int ActivationThreshold = 2;

    /// <summary>Messages published at the endpoint the kill switch is expected to stop.</summary>
    private const int PublishedAtStoppedEndpoint = 10;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long a stopped endpoint is given to prove it is not consuming. The kill switch is configured with a restart
    /// timeout far beyond this, so the endpoint cannot come back within the window.
    /// </summary>
    private static readonly TimeSpan StoppedWindow = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task Should_retry_a_throwing_consumer_and_finally_fault()
    {
        // Arrange
        var probe = new RetryProbe();
        var config = new TestConfig()
            .UseInMemoryBus()
            .UseInstanceType(InstanceType.Standalone)
            .SetSetting("MessageBus:RetryIntervals:0", "1")
            .SetSetting("MessageBus:RetryIntervals:1", "1")
            .SetSetting("MessageBus:RetryIntervals:2", "1")
            .BuildConfiguration();

        await using var serviceProvider = SetupServiceProvider(config, probe, new OutboxProbe(succeedOnAttempt: 1));

        // The ladder is pinned to exactly what was configured rather than read back from the options the pipeline
        // uses. Deriving the expected attempt count from those options would make this test agree with whatever the
        // binder produced: restore a non-empty default on RetryIntervals, the binder appends to it, and a six-step
        // ladder would still "pass" - just slower. This is the only end-to-end test placed to catch that regression.
        serviceProvider.GetRequiredService<IOptions<MessageBusOptions>>().Value.RetryIntervals
            .Should().Equal([1, 1, 1], "a configured ladder replaces the default instead of being appended to it");

        const int expectedAttempts = 4;

        var bus = serviceProvider.GetRequiredService<IBusControl>();
        await bus.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            // Act
            await bus.Publish(new RetryProbeCommand(), TestContext.Current.CancellationToken);

            // Assert
            (await Completes(probe.FirstAttempt)).Should().BeTrue("the command has to reach the consumer");
            (await Completes(probe.Faulted)).Should().BeTrue("the exhausted retry ladder has to fault the message");
            probe.Attempts.Should().Be(expectedAttempts, "each of the three configured intervals adds one attempt to the initial one");
        }
        finally
        {
            await bus.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Should_not_retry_when_the_ladder_is_configured_empty()
    {
        // Arrange: an empty configuration value binds to an empty array, which is the operator's "do not retry"
        // (ADR-004 D2). This is the form that reaches a device, where configuration arrives as environment variables.
        var probe = new RetryProbe();
        var config = new TestConfig()
            .UseInMemoryBus()
            .UseInstanceType(InstanceType.Standalone)
            .SetSetting("MessageBus:RetryIntervals", string.Empty)
            .BuildConfiguration();

        await using var serviceProvider = SetupServiceProvider(config, probe, new OutboxProbe(succeedOnAttempt: 1));
        serviceProvider.GetRequiredService<IOptions<MessageBusOptions>>().Value.RetryIntervals
            .Should().BeEmpty("an empty configuration value has to bind to an empty ladder, not to null");

        var bus = serviceProvider.GetRequiredService<IBusControl>();
        await bus.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            // Act
            await bus.Publish(new RetryProbeCommand(), TestContext.Current.CancellationToken);

            // Assert
            (await Completes(probe.Faulted)).Should().BeTrue("the message still has to fault");
            probe.Attempts.Should().Be(1, "an empty ladder means the consumer is attempted exactly once");
        }
        finally
        {
            await bus.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Should_discard_what_a_failed_attempt_published()
    {
        // Arrange: the in-memory outbox has to sit inside the retry filter on the in-memory transport too (ADR-004 D1).
        // Without it every failed attempt republishes its side effects, so the event below would arrive once per attempt.
        var probe = new OutboxProbe(succeedOnAttempt: 3);
        var config = new TestConfig()
            .UseInMemoryBus()
            .UseInstanceType(InstanceType.Standalone)
            .SetSetting("MessageBus:RetryIntervals:0", "1")
            .SetSetting("MessageBus:RetryIntervals:1", "1")
            .BuildConfiguration();

        await using var serviceProvider = SetupServiceProvider(config, new RetryProbe(), probe);

        var bus = serviceProvider.GetRequiredService<IBusControl>();
        await bus.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            // Act
            await bus.Publish(new OutboxProbeCommand(), TestContext.Current.CancellationToken);

            // Assert
            (await Completes(probe.Succeeded)).Should().BeTrue("the third attempt has to succeed");
            (await Completes(probe.SideEffectReceived)).Should().BeTrue("the successful attempt has to flush its publish buffer");

            // The two failed attempts published before they threw, so their side effects get time to arrive.
            await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

            probe.Attempts.Should().Be(3, "two configured intervals add two attempts to the initial one");
            probe.SideEffects.Should().Be(1, "only the attempt that succeeded may publish; the two that threw are discarded");
        }
        finally
        {
            await bus.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Theory]
    [InlineData(nameof(ArgumentException), 1)]
    [InlineData(nameof(ArgumentNullException), 1)]
    [InlineData(nameof(NotSupportedException), 1)]
    [InlineData(nameof(InvalidOperationException), 4)]
    public async Task Should_not_retry_a_deterministic_failure(string exceptionType, int expectedAttempts)
    {
        // Arrange: ADR-004 (D2) - a malformed payload or a programming error goes to the error transport immediately
        // instead of consuming the whole ladder to fail identically every time. ArgumentNullException is included
        // because the ADR claims Ignore<ArgumentException>() covers its derived types; InvalidOperationException is
        // the control that has to be retried, so a test that stops distinguishing them fails.
        var probe = new RetryProbe { ThrownException = exceptionType };
        var config = new TestConfig()
            .UseInMemoryBus()
            .UseInstanceType(InstanceType.Standalone)
            .SetSetting("MessageBus:RetryIntervals:0", "1")
            .SetSetting("MessageBus:RetryIntervals:1", "1")
            .SetSetting("MessageBus:RetryIntervals:2", "1")
            .BuildConfiguration();

        await using var serviceProvider = SetupServiceProvider(config, probe, new OutboxProbe(succeedOnAttempt: 1));

        var bus = serviceProvider.GetRequiredService<IBusControl>();
        await bus.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            // Act
            await bus.Publish(new RetryProbeCommand(), TestContext.Current.CancellationToken);

            // Assert
            (await Completes(probe.Faulted)).Should().BeTrue("the message has to fault either way");
            probe.Attempts.Should().Be(expectedAttempts);
        }
        finally
        {
            await bus.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Should_apply_the_ladder_belonging_to_each_endpoint()
    {
        // Arrange: ADR-004 (D2). Each class gets a different number of intervals, so the attempt counts identify
        // which ladder actually reached which endpoint - classification is unit-tested, but nothing proved it is
        // wired through to the receive pipeline.
        //
        // The request probe additionally pins the documented fail-safe. Request is the only class that cannot be
        // recognized from the queue name, so it depends on FindRequestEndpoints seeing the consumer in
        // assembliesToScan. This harness scans only the SDK contract assembly and registers its probes explicitly,
        // so the request endpoint is invisible to that scan - and must therefore fall back to the default ladder
        // rather than to something shorter.
        var probe = new LadderProbe();
        var config = new TestConfig()
            .UseInMemoryBus()
            .UseInstanceType(InstanceType.Standalone)
            .SetSetting("MessageBus:RetryIntervals:0", "1")
            .SetSetting("MessageBus:InstanceQueueRetryIntervals:0", "1")
            .SetSetting("MessageBus:InstanceQueueRetryIntervals:1", "1")
            .SetSetting("MessageBus:RequestRetryIntervals:0", "1")
            .SetSetting("MessageBus:RequestRetryIntervals:1", "1")
            .SetSetting("MessageBus:RequestRetryIntervals:2", "1")
            .BuildConfiguration();

        await using var serviceProvider = SetupServiceProvider(config, new RetryProbe(), new OutboxProbe(succeedOnAttempt: 1), probe);

        var bus = serviceProvider.GetRequiredService<IBusControl>();
        await bus.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            // Act
            await bus.Publish(new DefaultLadderCommand(), TestContext.Current.CancellationToken);
            await bus.Publish(new InstanceLadderCommand { InstanceId = Guid.NewGuid() }, TestContext.Current.CancellationToken);
            await bus.Publish(new RequestLadderRequest(), TestContext.Current.CancellationToken);

            // Assert
            (await Completes(probe.AllFaulted)).Should().BeTrue("all three messages have to exhaust their ladder");

            probe.DefaultAttempts.Should().Be(2, "the shared Commands queue takes the default ladder of one interval");
            probe.InstanceAttempts.Should().Be(3, "the serialized instance queue takes its own ladder of two intervals");
            probe.RequestAttempts.Should().Be(2,
                "an endpoint the request scan cannot see falls back to the default ladder, not to the shorter request one");
        }
        finally
        {
            await bus.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Should_give_every_attempt_its_own_dependency_scope()
    {
        // Arrange: ADR-004 (D1). The message scope has to sit inside the retry filter. Configured on the bus it wraps
        // the endpoint filters instead, and every attempt of one message resolves from a single DI scope - so a
        // consumer that failed inside SaveChangesAsync would retry on the very same DbContext with the change tracker
        // still dirty, and fail identically every time without anything looking broken.
        var probe = new ScopeProbe();
        var config = new TestConfig()
            .UseInMemoryBus()
            .UseInstanceType(InstanceType.Standalone)
            .SetSetting("MessageBus:RetryIntervals:0", "1")
            .SetSetting("MessageBus:RetryIntervals:1", "1")
            .BuildConfiguration();

        await using var serviceProvider = SetupServiceProvider(config, new RetryProbe(), new OutboxProbe(succeedOnAttempt: 1),
            scopeProbe: probe);

        var bus = serviceProvider.GetRequiredService<IBusControl>();
        await bus.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            // Act
            await bus.Publish(new ScopeProbeCommand(), TestContext.Current.CancellationToken);
            await Task.Delay(StoppedWindow, TestContext.Current.CancellationToken);

            // Assert
            probe.ScopeIds.Should().HaveCount(3, "two configured intervals add two attempts to the initial one");
            probe.ScopeIds.Distinct().Should().HaveCount(3, "each attempt has to resolve its scoped dependencies afresh");
        }
        finally
        {
            await bus.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Should_stop_an_endpoint_whose_messages_keep_failing()
    {
        // Arrange: ADR-004 (D4). Retry is switched off so every message counts as exactly one attempt and one
        // failure, and the restart timeout is set far beyond the observation window so a tripped switch stays tripped.
        var probe = new RetryProbe();
        var config = new TestConfig()
            .UseInMemoryBus()
            .UseInstanceType(InstanceType.Standalone)
            .SetSetting("MessageBus:RetryIntervals", string.Empty)
            .SetSetting("MessageBus:KillSwitch:ActivationThreshold", $"{ActivationThreshold}")
            .SetSetting("MessageBus:KillSwitch:TripThresholdPercent", "50")
            .SetSetting("MessageBus:KillSwitch:TrackingPeriodInSeconds", "60")
            .SetSetting("MessageBus:KillSwitch:RestartTimeoutInSeconds", "3600")
            .BuildConfiguration();

        await using var serviceProvider = SetupServiceProvider(config, probe, new OutboxProbe(succeedOnAttempt: 1));

        var bus = serviceProvider.GetRequiredService<IBusControl>();
        await bus.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            // Act: publish far more failing messages than the endpoint may consume before it is stopped
            for (var i = 0; i < PublishedAtStoppedEndpoint; i++)
                await bus.Publish(new RetryProbeCommand(), TestContext.Current.CancellationToken);

            (await Completes(probe.FirstAttempt)).Should().BeTrue("the endpoint has to start consuming");
            var consumed = await WaitUntilConsumptionStops(probe);

            // Assert: the exact trip point is not asserted - MassTransit arms the switch once ActivationThreshold
            // messages have been consumed and trips it on the failure after that, but messages already dispatched when
            // it trips still run, so the count varies. What has to hold is that the endpoint stopped rather than
            // draining its queue into the error transport.
            consumed.Should().BeLessThan(PublishedAtStoppedEndpoint,
                "the kill switch has to stop the endpoint, leaving the remaining messages queued");
        }
        finally
        {
            await bus.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Waits until the endpoint has stopped consuming - two samples a <see cref="StoppedWindow"/> apart with no
    /// progress - and returns how many messages it got through.
    /// </summary>
    private static async Task<int> WaitUntilConsumptionStops(RetryProbe probe)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (true)
        {
            var before = probe.Attempts;
            await Task.Delay(StoppedWindow, TestContext.Current.CancellationToken);
            var after = probe.Attempts;

            if (before == after || DateTime.UtcNow > deadline)
                return after;
        }
    }

    private static async Task<bool> Completes(Task task)
        => await Task.WhenAny(task, Task.Delay(Timeout, TestContext.Current.CancellationToken)) == task;

    private static ServiceProvider SetupServiceProvider(IConfiguration config, RetryProbe probe, OutboxProbe outboxProbe,
        LadderProbe? ladderProbe = null, ScopeProbe? scopeProbe = null)
    {
        var moduleManager = Substitute.For<IModuleHost>();
        moduleManager.GetContext().Returns(new SuiteDependencyContext(new ModuleDependencyContext(ModuleType.Backend, "jsonPath", true), null, []));

        return new ServiceCollection()
            .AddLogging()
            .AddSingleton(moduleManager)
            .AddSingleton(config)
            .AddSingleton(probe)
            .AddSingleton(outboxProbe)
            .AddSingleton(ladderProbe ?? new LadderProbe())
            .AddSingleton(scopeProbe ?? new ScopeProbe())
            .AddScoped<ScopeMarker>()
            .AddInstanceServicesMock()
            .AddOptions<MessageBusOptions>()
            .BindConfiguration(MessageBusOptions.ConfigSection)
            .Services
            .AddMassTransitMessageBus(config,
                cfg =>
                {
                    cfg.AddConsumer<RetryProbeConsumer>();
                    cfg.AddConsumer<RetryProbeFaultConsumer>();
                    cfg.AddConsumer<OutboxProbeConsumer>();
                    cfg.AddConsumer<OutboxProbeSideEffectConsumer>();
                    cfg.AddConsumer<DefaultLadderProbeConsumer>();
                    cfg.AddConsumer<InstanceLadderProbeConsumer>();
                    cfg.AddConsumer<RequestLadderProbeConsumer>();
                    cfg.AddConsumer<LadderFaultConsumer>();
                    cfg.AddConsumer<ScopeProbeConsumer>();
                },
                // The SDK contract assembly holds no consumers, so only the probes above are registered.
                [typeof(ICommand).Assembly])
            .BuildServiceProvider();
    }

    private sealed record RetryProbeCommand : ICommand
    {
        public Guid CorrelationId { get; init; } = Guid.NewGuid();
    }

    private sealed class RetryProbe
    {
        private readonly TaskCompletionSource _faulted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _firstAttempt = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _attempts;

        /// <summary>Name of the exception type the consumer throws; defaults to a retryable one.</summary>
        public string ThrownException { get; init; } = nameof(InvalidOperationException);

        public int Attempts => Volatile.Read(ref _attempts);

        public Task Faulted => _faulted.Task;

        public Task FirstAttempt => _firstAttempt.Task;

        public void RecordAttempt()
        {
            Interlocked.Increment(ref _attempts);
            _firstAttempt.TrySetResult();
        }

        public void RecordFault() => _faulted.TrySetResult();
    }

    private sealed class RetryProbeConsumer(RetryProbe probe) : IConsumer<RetryProbeCommand>
    {
        public Task Consume(ConsumeContext<RetryProbeCommand> context)
        {
            probe.RecordAttempt();

            throw probe.ThrownException switch
            {
                nameof(ArgumentException) => new ArgumentException("probe failure"),
                nameof(ArgumentNullException) => new ArgumentNullException(nameof(context), "probe failure"),
                nameof(NotSupportedException) => new NotSupportedException("probe failure"),
                _ => new InvalidOperationException("probe failure")
            };
        }
    }

    private sealed class RetryProbeFaultConsumer(RetryProbe probe) : IConsumer<Fault<RetryProbeCommand>>
    {
        public Task Consume(ConsumeContext<Fault<RetryProbeCommand>> context)
        {
            probe.RecordFault();
            return Task.CompletedTask;
        }
    }

    private sealed record OutboxProbeCommand : ICommand
    {
        public Guid CorrelationId { get; init; } = Guid.NewGuid();
    }

    private sealed record OutboxProbeSideEffect : IEvent
    {
        public Guid CorrelationId { get; init; } = Guid.NewGuid();
    }

    private sealed class OutboxProbe(int succeedOnAttempt)
    {
        private readonly TaskCompletionSource _sideEffectReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _succeeded = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _attempts;
        private int _sideEffects;

        public int Attempts => Volatile.Read(ref _attempts);

        public int SideEffects => Volatile.Read(ref _sideEffects);

        public Task SideEffectReceived => _sideEffectReceived.Task;

        public Task Succeeded => _succeeded.Task;

        public bool RecordAttempt() => Interlocked.Increment(ref _attempts) >= succeedOnAttempt;

        public void RecordSuccess() => _succeeded.TrySetResult();

        public void RecordSideEffect()
        {
            Interlocked.Increment(ref _sideEffects);
            _sideEffectReceived.TrySetResult();
        }
    }

    private sealed class OutboxProbeConsumer(OutboxProbe probe) : IConsumer<OutboxProbeCommand>
    {
        public async Task Consume(ConsumeContext<OutboxProbeCommand> context)
        {
            var shouldSucceed = probe.RecordAttempt();

            // Publish first, fail afterwards: the shape the in-memory outbox exists to protect.
            await context.Publish(new OutboxProbeSideEffect(), context.CancellationToken);

            if (!shouldSucceed)
                throw new InvalidOperationException("probe failure after publish");

            probe.RecordSuccess();
        }
    }

    private sealed class OutboxProbeSideEffectConsumer(OutboxProbe probe) : IConsumer<OutboxProbeSideEffect>
    {
        public Task Consume(ConsumeContext<OutboxProbeSideEffect> context)
        {
            probe.RecordSideEffect();
            return Task.CompletedTask;
        }
    }

    private sealed record DefaultLadderCommand : ICommand
    {
        public Guid CorrelationId { get; init; } = Guid.NewGuid();
    }

    private sealed record InstanceLadderCommand : IInstanceDependentCommand
    {
        public Guid CorrelationId { get; init; } = Guid.NewGuid();

        public Guid InstanceId { get; init; }
    }

    private sealed record LadderResponse : IResponse
    {
        public ErrorInfo? RequestError { get; init; }
    }

    private sealed record RequestLadderRequest : IRequest<LadderResponse>
    {
        public Guid CorrelationId { get; init; } = Guid.NewGuid();
    }

    /// <summary>
    /// Counts the delivery attempts each retry class produced, so the attempt counts identify which ladder reached
    /// which endpoint. See ADR-004 (D2).
    /// </summary>
    private sealed class LadderProbe
    {
        private readonly TaskCompletionSource _allFaulted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _defaultAttempts;
        private int _instanceAttempts;
        private int _requestAttempts;
        private int _faults;

        public int DefaultAttempts => Volatile.Read(ref _defaultAttempts);

        public int InstanceAttempts => Volatile.Read(ref _instanceAttempts);

        public int RequestAttempts => Volatile.Read(ref _requestAttempts);

        public Task AllFaulted => _allFaulted.Task;

        public void RecordDefault() => Interlocked.Increment(ref _defaultAttempts);

        public void RecordInstance() => Interlocked.Increment(ref _instanceAttempts);

        public void RecordRequest() => Interlocked.Increment(ref _requestAttempts);

        public void RecordFault()
        {
            if (Interlocked.Increment(ref _faults) == 3)
                _allFaulted.TrySetResult();
        }
    }

    private sealed class DefaultLadderProbeConsumer(LadderProbe probe) : IConsumer<DefaultLadderCommand>
    {
        public Task Consume(ConsumeContext<DefaultLadderCommand> context)
        {
            probe.RecordDefault();
            throw new InvalidOperationException("default ladder probe");
        }
    }

    [ReadOnlyConsumer]
    private sealed class InstanceLadderProbeConsumer(LadderProbe probe) : IConsumer<InstanceLadderCommand>
    {
        public Task Consume(ConsumeContext<InstanceLadderCommand> context)
        {
            probe.RecordInstance();
            throw new InvalidOperationException("instance ladder probe");
        }
    }

    /// <summary>
    /// A raw <see cref="IConsumer{T}"/> rather than a <c>RequestConsumer&lt;,&gt;</c>: the base class answers instead of
    /// throwing (ADR-004 D6a), so only a consumer that throws can reach the request ladder at all.
    /// </summary>
    private sealed class RequestLadderProbeConsumer(LadderProbe probe) : IConsumer<RequestLadderRequest>
    {
        public Task Consume(ConsumeContext<RequestLadderRequest> context)
        {
            probe.RecordRequest();
            throw new InvalidOperationException("request ladder probe");
        }
    }

    private sealed class LadderFaultConsumer(LadderProbe probe)
        : IConsumer<Fault<DefaultLadderCommand>>, IConsumer<Fault<InstanceLadderCommand>>, IConsumer<Fault<RequestLadderRequest>>
    {
        public Task Consume(ConsumeContext<Fault<DefaultLadderCommand>> context)
        {
            probe.RecordFault();
            return Task.CompletedTask;
        }

        public Task Consume(ConsumeContext<Fault<InstanceLadderCommand>> context)
        {
            probe.RecordFault();
            return Task.CompletedTask;
        }

        public Task Consume(ConsumeContext<Fault<RequestLadderRequest>> context)
        {
            probe.RecordFault();
            return Task.CompletedTask;
        }
    }
}

internal sealed record ScopeProbeCommand : ICommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}

/// <summary>Scoped marker whose identity reveals which DI scope a delivery attempt resolved from.</summary>
internal sealed class ScopeMarker
{
    public Guid Id { get; } = Guid.NewGuid();
}

internal sealed class ScopeProbe
{
    private readonly List<Guid> _scopeIds = [];

    public IReadOnlyList<Guid> ScopeIds
    {
        get
        {
            lock (_scopeIds)
                return [.. _scopeIds];
        }
    }

    public void Record(Guid scopeId)
    {
        lock (_scopeIds)
            _scopeIds.Add(scopeId);
    }
}

internal sealed class ScopeProbeConsumer(ScopeProbe probe, ScopeMarker marker) : IConsumer<ScopeProbeCommand>
{
    public Task Consume(ConsumeContext<ScopeProbeCommand> context)
    {
        probe.Record(marker.Id);
        throw new InvalidOperationException("scope probe failure");
    }
}
