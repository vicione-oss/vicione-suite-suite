using AwesomeAssertions;
using Core.OS.MessageBus.MassTransit.Configuration;
using MassTransit;
using NSubstitute;
using Xunit;

namespace Core.OS.Tests.MessageBus.MassTransit;

/// <summary>
/// Verifies the bounds applied to the lazily declared <c>_error</c> and <c>_skipped</c> queues, ADR-004 (D5). Without
/// them a master keeps its fault history forever, because those queues inherit the input queue's arguments and a
/// master deliberately has no queue expiration.
/// </summary>
public class FaultQueueTopologyTests
{
    private readonly IRabbitMqQueueBindingConfigurator _queue = Substitute.For<IRabbitMqQueueBindingConfigurator>();

    [Fact]
    public void Should_bound_a_fault_queue_with_the_default_settings()
    {
        // Arrange
        var settings = new ErrorQueueSettings();

        // Act
        FaultQueueTopology.Configure(_queue, settings);

        // Assert
        _queue.Received(1).SetQueueArgument("x-message-ttl", (long)TimeSpan.FromDays(settings.RetentionInDays).TotalMilliseconds);
        _queue.Received(1).SetQueueArgument("x-max-length", (long)settings.MaxMessages);
        _queue.Received(1).SetQueueArgument("x-max-length-bytes", (long)settings.MaxSizeInMegabytes * 1024 * 1024);
        _queue.Received().QueueExpiration = TimeSpan.FromDays(settings.RetentionInDays);
    }

    [Fact]
    public void Should_drop_the_oldest_fault_on_overflow()
    {
        // Arrange: reject-publish would make the error transport itself fail and turn a poison message into a
        // redelivery loop, so a bounded fault queue has to drop its head
        var settings = new ErrorQueueSettings();

        // Act
        FaultQueueTopology.Configure(_queue, settings);

        // Assert
        _queue.Received(1).SetQueueArgument("x-overflow", "drop-head");
    }

    [Fact]
    public void Should_omit_every_argument_when_the_settings_are_zeroed()
    {
        // Arrange: zeroing a value is the documented escape hatch for a broker that already declared the queue with
        // different arguments - redeclaring one with changed arguments fails with PRECONDITION_FAILED
        var settings = new ErrorQueueSettings { RetentionInDays = 0, MaxMessages = 0, MaxSizeInMegabytes = 0 };

        // Act
        FaultQueueTopology.Configure(_queue, settings);

        // Assert
        _queue.DidNotReceive().SetQueueArgument(Arg.Any<string>(), Arg.Any<object>());
        _queue.DidNotReceiveWithAnyArgs().QueueExpiration = default;
    }

    [Fact]
    public void Should_not_set_an_overflow_policy_without_a_length_bound()
    {
        // Arrange: x-overflow only means something alongside x-max-length or x-max-length-bytes
        var settings = new ErrorQueueSettings { RetentionInDays = 7, MaxMessages = 0, MaxSizeInMegabytes = 0 };

        // Act
        FaultQueueTopology.Configure(_queue, settings);

        // Assert
        _queue.DidNotReceive().SetQueueArgument("x-overflow", Arg.Any<object>());
        _queue.Received(1).SetQueueArgument("x-message-ttl", Arg.Any<object>());
    }

    [Theory]
    [InlineData(1, 1024L * 1024)]
    [InlineData(2, 2L * 1024 * 1024)]
    [InlineData(1024, 1024L * 1024 * 1024)]
    public void Should_convert_the_size_bound_from_megabytes_to_bytes(int megabytes, long expectedBytes)
    {
        // Arrange
        var settings = new ErrorQueueSettings { MaxSizeInMegabytes = megabytes };

        // Act
        FaultQueueTopology.Configure(_queue, settings);

        // Assert: the multiplication has to happen in long, not int - 1024 MiB overflows an int
        _queue.Received(1).SetQueueArgument("x-max-length-bytes", expectedBytes);
    }
}
