using System.Collections.Frozen;
using AwesomeAssertions;
using Core.OS.Instance.Consumers;
using Core.OS.MessageBus.MassTransit;
using Core.OS.MessageBus.MassTransit.Configuration;
using Core.OS.Modules.Consumers;
using Core.OS.Persistence.Consumers;
using Xunit;

namespace Core.OS.Tests.MessageBus.MassTransit;

/// <summary>
/// Verifies the per message class retry ladder selection of ADR-004 (D2).
/// </summary>
public class MessageRetryClassifierTests
{
    private static readonly Guid InstanceId = Guid.NewGuid();

    private static readonly MessageBusOptions Options = new()
    {
        RetryIntervals = [1, 2, 3],
        InstanceQueueRetryIntervals = [4, 5],
        RequestRetryIntervals = [6]
    };

    [Fact]
    public void Should_classify_the_replication_endpoint_as_serialized_instance_queue()
    {
        // Arrange: replication shares the single instance queue with every instance-dependent command
        var queueName = SuiteEndpointNameFormatter.GetConsumerName(typeof(DbChangeSetConsumer), InstanceId);
        var requestEndpoints = MessageRetryClassifier.FindRequestEndpoints([typeof(DbChangeSetConsumer).Assembly], InstanceId);

        // Act
        var retryClass = MessageRetryClassifier.Classify(queueName, requestEndpoints);

        // Assert
        retryClass.Should().Be(MessageRetryClass.SerializedInstance);
    }

    [Fact]
    public void Should_classify_an_instance_dependent_command_endpoint_as_serialized_instance_queue()
    {
        // Arrange
        var queueName = SuiteEndpointNameFormatter.GetConsumerName(typeof(EnqueueModulePackageOperationsConsumer), InstanceId);
        var requestEndpoints = MessageRetryClassifier.FindRequestEndpoints([typeof(EnqueueModulePackageOperationsConsumer).Assembly], InstanceId);

        // Act
        var retryClass = MessageRetryClassifier.Classify(queueName, requestEndpoints);

        // Assert
        retryClass.Should().Be(MessageRetryClass.SerializedInstance);
    }

    [Fact]
    public void Should_classify_a_request_endpoint_as_request()
    {
        // Arrange
        var queueName = SuiteEndpointNameFormatter.GetConsumerName(typeof(GetInstancesConsumer), InstanceId);
        var requestEndpoints = MessageRetryClassifier.FindRequestEndpoints([typeof(GetInstancesConsumer).Assembly], InstanceId);

        // Act
        var retryClass = MessageRetryClassifier.Classify(queueName, requestEndpoints);

        // Assert
        retryClass.Should().Be(MessageRetryClass.Request);
    }

    [Fact]
    public void Should_keep_the_instance_queue_serialized_even_when_it_is_listed_as_a_request_endpoint()
    {
        // Arrange: instance-dependent requests resolve to the instance queue itself - GetEnvironmentOverridesConsumer
        // does. They stay out of the request set today only because IInstanceDependentRequest<T> does not extend
        // IRequest<T>. Should that ever change, a request-first order would drop the whole serialized queue,
        // replication included, onto the shortest ladder.
        var instanceQueue = SuiteEndpointNameFormatter.GetConsumerName(typeof(DbChangeSetConsumer), InstanceId);
        var requestEndpoints = new[] { instanceQueue }.ToFrozenSet(StringComparer.Ordinal);

        // Act
        var retryClass = MessageRetryClassifier.Classify(instanceQueue, requestEndpoints);

        // Assert
        retryClass.Should().Be(MessageRetryClass.SerializedInstance);
    }

    [Fact]
    public void Should_classify_a_fault_consumer_endpoint_as_default()
    {
        // Arrange: Fault<T> is generic, so the consumer reports no message types and the pinned SDK's ConsumesRequest
        // is vacuously true for it. Must keep passing after the SDK bump collapses ConsumesOnlyRequests.
        var queueName = SuiteEndpointNameFormatter.GetConsumerName(typeof(EnqueueModulePackageOperationsFaultConsumer), InstanceId);
        var requestEndpoints = MessageRetryClassifier.FindRequestEndpoints([typeof(EnqueueModulePackageOperationsFaultConsumer).Assembly], InstanceId);

        // Act
        var retryClass = MessageRetryClassifier.Classify(queueName, requestEndpoints);

        // Assert: a fault consumer is not a request consumer and must not get the shortest ladder
        retryClass.Should().Be(MessageRetryClass.Default);
    }

    [Fact]
    public void Should_not_collect_consumer_definitions_as_request_endpoints()
    {
        // Arrange: the scan is seeded with RegistrationMetadata.IsConsumerOrDefinition, so ConsumerDefinition<T>
        // types are in it too - and they implement no IConsumer<> at all, so they report no message types either.

        // Act
        var requestEndpoints = MessageRetryClassifier.FindRequestEndpoints([typeof(DbChangeSetConsumer).Assembly], InstanceId);

        // Assert
        requestEndpoints.Should()
            .NotContain(SuiteEndpointNameFormatter.GetConsumerName(typeof(DbChangeSetConsumerDefinition), InstanceId))
            .And.Contain(SuiteEndpointNameFormatter.GetConsumerName(typeof(GetInstancesConsumer), InstanceId),
                "real request consumers still have to be found");
    }

    [Fact]
    public void Should_classify_an_unknown_endpoint_as_default()
    {
        // Arrange
        var requestEndpoints = MessageRetryClassifier.FindRequestEndpoints([typeof(GetInstancesConsumer).Assembly], InstanceId);

        // Act
        var retryClass = MessageRetryClassifier.Classify("SomeUnknownQueue", requestEndpoints);

        // Assert: the default is the longest ladder, so an unrecognized endpoint fails safe
        retryClass.Should().Be(MessageRetryClass.Default);
    }

    [Fact]
    public void Should_select_the_default_ladder_for_commands_and_events()
    {
        // Act
        var intervals = MessageRetryClassifier.GetRetryIntervals(Options, MessageRetryClass.Default);

        // Assert
        intervals.Should().Equal(Options.RetryIntervals);
    }

    [Fact]
    public void Should_select_the_short_ladder_for_the_serialized_instance_queue()
    {
        // Act
        var intervals = MessageRetryClassifier.GetRetryIntervals(Options, MessageRetryClass.SerializedInstance);

        // Assert
        intervals.Should().Equal(Options.InstanceQueueRetryIntervals);
    }

    [Fact]
    public void Should_select_the_request_ladder_for_requests()
    {
        // Act
        var intervals = MessageRetryClassifier.GetRetryIntervals(Options, MessageRetryClass.Request);

        // Assert
        intervals.Should().Equal(Options.RequestRetryIntervals);
    }

    [Fact]
    public void Should_let_the_kill_switch_arm_on_an_idle_edge_endpoint()
    {
        // Arrange: ADR-004 (D4). Activation is a rate, not a count - ActivationThreshold messages have to be consumed
        // within one TrackingPeriod, and the kill switch counts messages rather than delivery attempts. Two
        // independent limits decide whether that is reachable on an idle edge node, which is exactly the case D4
        // exists for, and the threshold has to clear both.
        const int quietNodeMessagesPerMinute = 1;
        var killSwitch = new MessageBusOptions().KillSwitch;
        var trackingPeriod = TimeSpan.FromSeconds(killSwitch.TrackingPeriodInSeconds);

        // Act
        // 1. Arrival: a quiet node's instance queue sees roughly one message a minute, because health information is
        //    published on the health check period (60 s).
        var messagesArriving = (int)trackingPeriod.TotalMinutes * quietNodeMessagesPerMinute;

        // 2. Throughput: under a sustained dependency failure each delivery burns its whole retry ladder before it
        //    faults, and ConcurrentMessageLimit = 1 on Standalone means one delivery at a time per endpoint. So the
        //    longest ladder caps how many messages an endpoint can even get through in one tracking period. This is
        //    why the two kill-switch numbers cannot be read independently of the D2 ladders.
        var longestLadderMs = new[]
        {
            MessageRetryClassifier.DefaultIntervals.Sum(),
            MessageRetryClassifier.SerializedInstanceIntervals.Sum(),
            MessageRetryClassifier.RequestIntervals.Sum()
        }.Max();
        var messagesConsumable = (int)(trackingPeriod.TotalMilliseconds / longestLadderMs);

        // Assert
        killSwitch.ActivationThreshold.Should().BeLessThanOrEqualTo(messagesArriving,
            "the threshold has to be reachable at the arrival rate of an idle node");
        killSwitch.ActivationThreshold.Should().BeLessThanOrEqualTo(messagesConsumable,
            "a serialized endpoint burning the longest ladder per delivery has to still reach the threshold");
    }

    [Fact]
    public void Should_keep_every_default_ladder_short_enough_for_a_shared_queue()
    {
        // Arrange: ADR-004 (D2, Finding 5) - every endpoint is a shared, serialized queue, so a retry blocks the
        // message types queued behind it. No built-in ladder may stall a queue for longer than this budget.
        var budget = TimeSpan.FromSeconds(10);

        // Act
        int[][] ladders =
        [
            MessageRetryClassifier.DefaultIntervals,
            MessageRetryClassifier.SerializedInstanceIntervals,
            MessageRetryClassifier.RequestIntervals
        ];

        // Assert
        foreach (var ladder in ladders)
            TimeSpan.FromMilliseconds(ladder.Sum()).Should().BeLessThanOrEqualTo(budget);
    }

    [Fact]
    public void Should_disable_retry_for_an_explicitly_empty_ladder()
    {
        // Arrange: null means "not configured", an empty array means "do not retry" - the binder can express both,
        // an absent section binding to null and an empty configuration value binding to an empty array.
        var options = new MessageBusOptions
        {
            RetryIntervals = [],
            InstanceQueueRetryIntervals = [],
            RequestRetryIntervals = []
        };

        // Act + Assert
        MessageRetryClassifier.GetRetryIntervals(options, MessageRetryClass.Default).Should().BeEmpty();
        MessageRetryClassifier.GetRetryIntervals(options, MessageRetryClass.SerializedInstance).Should().BeEmpty();
        MessageRetryClassifier.GetRetryIntervals(options, MessageRetryClass.Request).Should().BeEmpty();
    }

    [Fact]
    public void Should_fall_back_to_the_default_ladders_when_nothing_is_configured()
    {
        // Arrange: the ladders default to null, both so the configuration binder cannot append to a non-empty
        // default and so an empty array stays free to mean "do not retry"
        var options = new MessageBusOptions();

        // Act
        var intervals = MessageRetryClassifier.GetRetryIntervals(options, MessageRetryClass.Default);

        // Assert
        intervals.Should().Equal(MessageRetryClassifier.DefaultIntervals);
        MessageRetryClassifier.GetRetryIntervals(options, MessageRetryClass.SerializedInstance)
            .Should().Equal(MessageRetryClassifier.SerializedInstanceIntervals);
        MessageRetryClassifier.GetRetryIntervals(options, MessageRetryClass.Request)
            .Should().Equal(MessageRetryClassifier.RequestIntervals);
    }
}
