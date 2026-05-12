using MassTransit;
using MassTransit.Configuration;

namespace Core.OS.Connections.Consumers;

public sealed class CreateConnectionConsumerDefinition : ConsumerDefinition<CreateConnectionConsumer>
{
    public CreateConnectionConsumerDefinition()
    {
        EndpointDefinition
            = new ConsumerEndpointDefinition<CreateConnectionConsumer>(
                new EndpointSettings<IEndpointDefinition<CreateConnectionConsumer>> { PrefetchCount = 1 });
        ConcurrentMessageLimit = 1;
    }
}
