using System.Collections.Frozen;
using Core.Module;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.OS.MessageBus.MassTransit;
using Core.OS.MessageBus.MassTransit.Configuration;
using Core.OS.Modules;
using Core.OS.Tests.Extensions;
using Core.Shared.Messaging;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sdk.Backend.Messaging;
using Sdk.Instance;
using Sdk.Messaging;
using Sdk.Modules;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.MessageBus.MassTransit;

public class EventForwardToUiConsumerDefinitionTests
{
    private static readonly Guid InstanceId = Guid.NewGuid();

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private static EventForwardToUiConsumerDefinition<ForwardedEvent> CreateDefinition(InstanceType instanceType)
    {
        var informationProvider = Substitute.For<ILocalInstanceInformationProvider>();
        informationProvider.ReadLocalInstanceId().Returns(InstanceId);

        return new EventForwardToUiConsumerDefinition<ForwardedEvent>(informationProvider, Options.Create(new InstanceOptions
        {
            HomeDirectory = "home",
            CacheDirectory = "cache",
            BackupDirectory = "backup",
            Type = instanceType
        }));
    }

    private static string GetEndpointName(InstanceType instanceType)
    {
        IConsumerDefinition definition = CreateDefinition(instanceType);

        return definition.GetEndpointName(Substitute.For<IEndpointNameFormatter>());
    }

    [Fact]
    public void Should_use_an_own_endpoint_on_a_standalone_instance()
    {
        // Act
        var endpointName = GetEndpointName(InstanceType.Standalone);

        // Assert
        endpointName.Should().Be($"{EventForwardToUiConsumerDefinition<ForwardedEvent>.QueueNamePrefix}_{InstanceId}");
    }

    [Fact]
    public void Should_not_name_the_standalone_endpoint_like_the_instance_queue()
    {
        // Act
        var endpointName = GetEndpointName(InstanceType.Standalone);

        // Assert
        endpointName.Should().NotStartWith(MessagingHelper.InstanceQueueNamePrefix);
    }

    [Fact]
    public void Should_give_the_standalone_endpoint_the_default_retry_ladder()
    {
        // Arrange
        var endpointName = GetEndpointName(InstanceType.Standalone);

        // Act
        var retryClass = MessageRetryClassifier.Classify(endpointName, FrozenSet<string>.Empty);

        // Assert
        retryClass.Should().Be(MessageRetryClass.Default);
    }

    [Theory]
    [InlineData(InstanceType.Master)]
    [InlineData(InstanceType.Slave)]
    public void Should_keep_the_instance_queue_on_a_clustered_instance(InstanceType instanceType)
    {
        // Act
        var endpointName = GetEndpointName(instanceType);

        // Assert
        endpointName.Should().Be($"{MessagingHelper.InstanceQueueNamePrefix}_{InstanceId}");
    }

    [Fact]
    public async Task Should_forward_an_event_while_an_instance_command_is_still_running_on_a_standalone_instance()
    {
        // Arrange
        var probe = new ForwardingProbe();
        var config = new TestConfig()
            .UseInMemoryBus()
            .UseInstanceType(InstanceType.Standalone)
            .BuildConfiguration();

        await using var serviceProvider = SetupServiceProvider(config, probe);
        var localInstanceId = serviceProvider.GetRequiredService<ILocalInstanceInformationProvider>().ReadLocalInstanceId();

        var bus = serviceProvider.GetRequiredService<IBusControl>();
        await bus.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            await bus.Publish(new BlockingInstanceCommand { InstanceId = localInstanceId }, TestContext.Current.CancellationToken);
            (await Completes(probe.CommandStarted)).Should().BeTrue("the instance command has to occupy the instance queue");

            // Act
            await bus.Publish(new ForwardedEvent(Guid.NewGuid()), TestContext.Current.CancellationToken);

            // Assert
            (await Completes(probe.EventForwarded)).Should().BeTrue("the forwarded event must not wait for the instance command");
        }
        finally
        {
            probe.ReleaseCommand();
            await bus.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    private static async Task<bool> Completes(Task task)
        => await Task.WhenAny(task, Task.Delay(Timeout, TestContext.Current.CancellationToken)) == task;

    private static ServiceProvider SetupServiceProvider(IConfiguration config, ForwardingProbe probe)
    {
        var moduleManager = Substitute.For<IModuleHost>();
        moduleManager.GetContext().Returns(new SuiteDependencyContext(new ModuleDependencyContext(ModuleType.Backend, "jsonPath", true), null, []));

        return new ServiceCollection()
            .AddLogging()
            .AddSingleton(moduleManager)
            .AddSingleton(config)
            .AddSingleton(probe)
            .AddSingleton<IUiEventPublisher<ForwardedEvent>, ProbeUiEventPublisher>()
            .AddInstanceServicesMock()
            .AddSingleton(Options.Create(config.GetInstanceOptions()))
            .AddOptions<MessageBusOptions>()
            .BindConfiguration(MessageBusOptions.ConfigSection)
            .Services
            .AddMassTransitMessageBus(config,
                cfg => cfg.AddConsumer<BlockingInstanceCommandConsumer>(),
                // The SDK contract assembly holds no consumers, so only the probe above is registered.
                [typeof(ICommand).Assembly])
            .BuildServiceProvider();
    }

    /// <summary>
    /// Public so the scan for <see cref="ForwardToUIAttribute"/> registers a forwarding consumer for it.
    /// </summary>
    [ForwardToUI]
    public sealed record ForwardedEvent(Guid CorrelationId) : IEvent, CorrelatedBy<Guid>;

    private sealed record BlockingInstanceCommand : IInstanceDependentCommand
    {
        public Guid CorrelationId { get; init; } = Guid.NewGuid();

        public Guid InstanceId { get; init; }
    }

    private sealed class ForwardingProbe
    {
        private readonly TaskCompletionSource _commandStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _commandReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _eventForwarded = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task CommandStarted => _commandStarted.Task;

        public Task EventForwarded => _eventForwarded.Task;

        public Task RunCommand()
        {
            _commandStarted.TrySetResult();
            return _commandReleased.Task;
        }

        public void ReleaseCommand() => _commandReleased.TrySetResult();

        public void RecordForwardedEvent() => _eventForwarded.TrySetResult();
    }

    [ReadOnlyConsumer]
    private sealed class BlockingInstanceCommandConsumer(ForwardingProbe probe) : IConsumer<BlockingInstanceCommand>
    {
        public Task Consume(ConsumeContext<BlockingInstanceCommand> context) => probe.RunCommand();
    }

    private sealed class ProbeUiEventPublisher(ForwardingProbe probe) : IUiEventPublisher<ForwardedEvent>
    {
        public Task PublishUiEvent(ForwardedEvent eventToPublish, Guid? correlationId, CancellationToken cancellationToken = default)
        {
            probe.RecordForwardedEvent();
            return Task.CompletedTask;
        }
    }
}
