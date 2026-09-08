using Core.OS.EnvironmentOverrides.Consumers;
using Core.OS.MessageBus.MassTransit;

namespace Core.OS.Tests.EnvironmentOverrides.Consumers;

public sealed class EnvironmentOverridesConsumerEndpointTests
{
    /// <summary>
    /// Both consumers must land on the per-instance queue. A consumer on a shared <c>Commands</c> / <c>Requests</c>
    /// queue is served by the master, so the panel would read one node's file and write another's.
    /// </summary>
    [Theory]
    [InlineData(typeof(GetEnvironmentOverridesConsumer))]
    [InlineData(typeof(SetEnvironmentOverridesConsumer))]
    public void Should_be_routed_to_the_queue_of_the_instance_it_runs_on(Type consumerType)
    {
        // Arrange
        var instanceId = Guid.NewGuid();

        // Act
        var endpointName = SuiteEndpointNameFormatter.GetConsumerName(consumerType, instanceId);

        // Assert
        endpointName.Should().Be($"Instance_{instanceId}");
    }
}
