using Core.OS.Instance;
using Core.OS.MessageBus.MassTransit;
using MassTransit;
using MassTransit.Courier.Contracts;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace Core.OS.Tests.MessageBus.MassTransit;

public class SuiteEndpointNameFormatterTests
{
    private const string ActivityEndpoint = "CustomActivityEndpoint";
    private const string CommandEndpoint = "CustomCommandEndpoint";
    private const string RequestEndpoint = "CustomRequestEndpoint";
    private const string EventEndpoint = "CustomEventEndpoint";

    private readonly Guid _instanceId = Guid.NewGuid();

    private SuiteEndpointNameFormatter CreateFormatter()
    {
        // Arrange
        var infoProvider = Substitute.For<ILocalInstanceInformationProvider>();
        infoProvider.ReadLocalInstanceId().Returns(_instanceId);

        return new SuiteEndpointNameFormatter(infoProvider);
    }

    private static string GetTestTypeName()
    {
        var testType = typeof(SuiteEndpointNameFormatterTests);
        return $"{testType.Namespace!.Replace(".", string.Empty, StringComparison.Ordinal)}{testType.Name}";
    }

    public class TemporaryEndpoint : SuiteEndpointNameFormatterTests
    {
        [Fact]
        public void Should_return_temporary_endpoint()
        {
            // Act
            var tag = nameof(SimpleEvent);
            var endpoint = CreateFormatter().TemporaryEndpoint(tag);

            // Assert
            Assert.Contains(tag, endpoint, StringComparison.Ordinal);
        }
    }

    public class Consumer : SuiteEndpointNameFormatterTests
    {
        [Fact]
        public void Should_return_consumer_name()
        {
            // Act
            var consumer = CreateFormatter().Consumer<ValidCommandConsumer>();

            // Assert
            Assert.Equal("Commands", consumer);// MessagingHelper.CommandsQueueName is internal
        }
    }

    public class Message : SuiteEndpointNameFormatterTests
    {
        [Fact]
        public void Should_return_message_name()
        {
            // Act
            var message = CreateFormatter().Message<SimpleEvent>();

            // Assert
            Assert.Equal(nameof(SimpleEvent), message);
        }
    }

    public class GetConsumerName : SuiteEndpointNameFormatterTests
    {
        [Theory]
        [InlineData("Commands", typeof(ValidCommandConsumer))]
        [InlineData("Events", typeof(ValidMultiEventConsumer))]
        [InlineData("Commands", typeof(ValidTrackingConsumer))]
        public void Should_return_correct_consumer_name(string consumerName, Type consumerType)
        {
            // Act
            var consume = SuiteEndpointNameFormatter.GetConsumerName(consumerType, Guid.NewGuid());

            // Assert
            Assert.NotNull(consume);
            Assert.Equal($"{consumerName}", consume);
        }

        [Fact]
        public void Should_return_correct_consumer_name_for_own_endpoint_name_instance_independent()
        {
            // Arrange
            var consumerType = typeof(ConsumerActivityOwnEndpointInstanceIndependent);

            // Act
            var messageName = SuiteEndpointNameFormatter.GetConsumerName(consumerType, Guid.NewGuid());

            // Assert
            Assert.Equal($"{ActivityEndpoint}", messageName);
        }

        [Fact]
        public void Should_return_correct_consumer_name_for_own_endpoint_name_instance_dependent()
        {
            // Arrange
            var consumerType = typeof(ConsumerActivityOwnEndpointInstanceDependent);

            // Act
            var messageName = SuiteEndpointNameFormatter.GetConsumerName(consumerType, _instanceId);

            // Assert
            Assert.Equal($"{ActivityEndpoint}_{_instanceId}", messageName);
        }

        [Fact]
        public void Should_return_correct_consumer_command_name_for_own_endpoint()
        {
            // Arrange
            //var consumerType = typeof(ValidOwnEndpointCommandConsumer);

            // Act
            var consume = SuiteEndpointNameFormatter.GetConsumerName(typeof(ValidOwnEndpointCommandConsumer), _instanceId);

            // Assert
            Assert.NotNull(consume);
            Assert.Equal(CommandEndpoint, consume);
        }

        [Theory]
        [InlineData(typeof(ValidOwnEndpointInstanceDependentCommandConsumer))]
        [InlineData(typeof(ValidDefaultEndpointInstanceDependentCommandConsumer))]
        [InlineData(typeof(ValidInstanceDependentEventConsumer))]
        public void Should_return_correct_consumer_name_for_instance_dependent(Type consumerType)
        {
            // Arrange + Act
            var consume = SuiteEndpointNameFormatter.GetConsumerName(consumerType, _instanceId);

            // Assert
            Assert.NotNull(consume);
            Assert.Equal($"Instance_{_instanceId}", consume);
        }

        [Fact]
        public void Should_return_correct_default_consumer_name()
        {
            // Arrange
            var consumerType = typeof(ValidSomethingConsumer);
            var instanceId = Guid.NewGuid();

            // Act
            var consume = SuiteEndpointNameFormatter.GetConsumerName(consumerType, instanceId);

            // Assert
            Assert.NotNull(consume);
            Assert.Equal($"{GetTestTypeName()}{nameof(ValidSomethingConsumer)[..^"Consumer".Length]}", consume);
        }

        [Fact]
        public void Should_return_correct_default_generic_consumer_name()
        {
            // Arrange
            var consumerType = typeof(GenericSomethingConsumer<Something>);
            var instanceId = Guid.NewGuid();

            // Act
            var consume = SuiteEndpointNameFormatter.GetConsumerName(consumerType, instanceId);

            // Assert
            Assert.NotNull(consume);
            Assert.Equal($"{GetTestTypeName()}{nameof(Something)}", consume);
        }

        [Theory]
        [InlineData(typeof(InvalidOwnEndpointInstanceDependentCommandConsumer))]
        [InlineData(typeof(InvalidCommandAndEventConsumer))]
        [InlineData(typeof(InvalidCommandAndIndependentEventConsumer))]
        [InlineData(typeof(InvalidMultiEventConsumer))]
        [InlineData(typeof(InvalidMultiCommandConsumer))]
        public void Should_throw_invalid_consumer_name(Type consumerType)
            => Assert.Throws<InvalidOperationException>(()
                => SuiteEndpointNameFormatter.GetConsumerName(consumerType, Guid.NewGuid()));
    }

    public class GetMessageName : SuiteEndpointNameFormatterTests
    {


        [Fact]
        public void Should_return_correct_command_name_for_default_endpoint_name()
        {
            // Arrange + Act
            var messageName = CreateFormatter().Message<SomeCommand>();

            // Assert
            Assert.Equal(nameof(SomeCommand), messageName);
        }

        [Fact]
        public void Should_return_correct_command_name_for_own_endpoint_name()
        {
            // Arrange + Act
            var messageName = CreateFormatter().Message<OwnEndpointCommand>();

            // Assert
            Assert.Equal(CommandEndpoint, messageName);
        }

        [Fact]
        public void Should_return_correct_event_name_for_instance_independent()
        {
            // Arrange + Act
            var messageName = CreateFormatter().Message<InstanceIndependentEvent>();

            // Assert
            Assert.Equal(nameof(InstanceIndependentEvent), messageName);
        }

        [Fact]
        public void Should_return_correct_event_name_for_instance_dependent()
        {
            // Act
            var messageName = CreateFormatter().Message<InstanceDependentEvent>();

            // Assert
            Assert.Equal($"{nameof(InstanceDependentEvent)}_{_instanceId}", messageName);
        }

        [Fact]
        public void Should_return_correct_request_name_for_instance_independent()
        {
            // Arrange + Act
            var messageName = CreateFormatter().Message<ItemRequest>();

            // Assert
            Assert.Equal($"{nameof(ItemRequest)}", messageName);
        }

        [Fact]
        public void Should_return_correct_request_name_for_own_endpoint_name_instance_independent()
        {
            // Arrange + Act
            var messageName = CreateFormatter().Message<ItemOwnEndpointRequest>();

            // Assert
            Assert.Equal($"{RequestEndpoint}", messageName);
        }

        [Fact]
        public void Should_return_correct_request_name_for_own_endpoint_name_instance_dependent()
        {
            // Arrange + Act
            var messageName = CreateFormatter().Message<ItemOwnEndpointInstanceDependentRequest>();

            // Assert
            Assert.Equal($"{RequestEndpoint}_{_instanceId}", messageName);
        }

        [Fact]
        public void Should_return_correct_request_name_for_default_endpoint_name_instance_dependent()
        {
            // Arrange + Act
            var messageName = CreateFormatter().Message<ItemDefaultEndpointInstanceDependentRequest>();

            // Assert
            Assert.Equal($"{nameof(ItemDefaultEndpointInstanceDependentRequest)}_{_instanceId}", messageName);
        }

        [Fact]
        public void Should_return_correct_routable_message_name_for_attribute_endpoint_name_instance_dependent()
        {
            // Arrange + Act
            var messageName = CreateFormatter().Message<OwnEndpointInstanceDependentEvent>();

            // Assert
            Assert.Equal($"{EventEndpoint}_{_instanceId}", messageName);
        }

        [Fact]
        public void Should_return_incorrect_name()
        {
            // Arrange + Act
            var messageName = CreateFormatter().Message<ValidTrackingConsumer>();

            // Assert
            Assert.Equal(nameof(ValidTrackingConsumer), messageName);
        }

        [Fact]
        public void Should_return_correct_name_for_generic_type_without_custom_attribute()
        {
            // Arrange
            var joinedFullTypeName = typeof(SomeCommand).FullName!
                .Replace("+", "", StringComparison.Ordinal)
                .Replace(".", string.Empty, StringComparison.Ordinal);

            // Act
            var messageName = CreateFormatter().Message<GenericSomething<SomeCommand>>();

            // Assert
            Assert.Equal(joinedFullTypeName, messageName);
        }

        [Fact]
        public void Should_return_correct_name_for_generic_type_with_custom_attribute_default_endpoint_name()
        {
            // Arrange
            var genericType = typeof(GenericCommand<SomeCommand>);

            // Act
            var messageName = CreateFormatter().Message<GenericCommand<SomeCommand>>();

            // Assert
            Assert.Equal($"{genericType.Name}", messageName);
        }

        [Fact]
        public void Should_return_correct_name_for_generic_type_with_custom_attribute_own_endpoint_name()
        {
            // Arrange
            var genericType = typeof(OtherGenericCommand<SomeOtherCommand>);
            var genericTypeName = genericType.Name[..genericType.Name.IndexOf('`', StringComparison.Ordinal)];

            // Act
            var messageName = CreateFormatter().Message<OtherGenericCommand<SomeOtherCommand>>();

            // Assert
            Assert.Equal($"{CommandEndpoint}_{genericTypeName}", messageName);
        }
    }

    public class CompensateActivity : SuiteEndpointNameFormatterTests
    {
        [Fact]
        public void Should_return_correct_compensate_name_with_truncate_activity_word()
        {
            // Act
            var compensateActivityName = CreateFormatter()
                .CompensateActivity<SystemCompensateActivity, DataActivityOwnEndpointInstanceDependent>();

            // Assert
            Assert.Equal("__SystemCompensate_compensate", compensateActivityName);
        }

        [Fact]
        public void Should_return_compensate_activity_with_truncate_activity_word()
        {
            // Act
            var compensate
                = CreateFormatter().CompensateActivity<SystemCompensateActivity, DataActivityOwnEndpointInstanceDependent>();

            // Assert
            Assert.Equal("__SystemCompensate_compensate", compensate);
        }

        [Fact]
        public void Should_return_compensate_activity_without_truncate_activity_word()
        {
            // Act
            var compensate = CreateFormatter().CompensateActivity<TestActivitySpecial, ILogger>();

            // Assert
            Assert.Equal($"__{nameof(TestActivitySpecial)}_compensate", compensate);
        }
    }

    public class ExecuteActivity : SuiteEndpointNameFormatterTests
    {
        [Fact]
        public void Should_return_correct_execute_name_for_own_endpoint_name_instance_dependent()
        {
            // Arrange + Act
            var executeActivityName = CreateFormatter()
                .ExecuteActivity<SystemExecuteActivity, DataActivityOwnEndpointInstanceDependent>();

            // Assert
            Assert.Equal($"{ActivityEndpoint}_{_instanceId}", executeActivityName);
        }
    }

    public class Saga : SuiteEndpointNameFormatterTests
    {

        [Fact]
        public void Should_return_saga_name_routable()
        {
            // Act
            var sagaName = CreateFormatter().Saga<StateMachineSaga>();

            // Assert
            Assert.Equal($"{nameof(StateMachineSaga)}", sagaName);
        }

        [Fact]
        public void GetSagaName_should_return_valid_name()
            => Assert.NotNull(CreateFormatter().Saga<TestMachineState>());
    }

    private class ValidCommandConsumer : IConsumer<SomeCommand>
    {
        public Task Consume(ConsumeContext<SomeCommand> context)
            => Task.CompletedTask;
    }

    private class TestActivitySpecial : ICompensateActivity<ILogger>
    {
        public Task<CompensationResult> Compensate(CompensateContext<ILogger> context)
        {
            var resultMock = Substitute.For<CompensationResult>();
            return Task.FromResult(resultMock);
        }
    }

    private class StateMachineSaga : ISaga
    {
        public Guid CorrelationId { get; set; } = Guid.NewGuid();
    }

    private record SimpleEvent : IEvent;

    private class TestMachineState : SagaStateMachineInstance
    {
        public Guid CorrelationId { get; set; } = Guid.NewGuid();
    }

    [MessageEndpoint(EventEndpoint)]
    public sealed record OwnEndpointInstanceDependentEvent : IInstanceEvent;

    private record InstanceDependentEvent : IInstanceEvent;

    private record InstanceIndependentEvent : IEvent;

    private record OtherInstanceIndependentEvent : IEvent;

    private record SomeCommand(Guid CorrelationId) : ICommand;

    private record SomeOtherCommand(Guid CorrelationId) : ICommand;

    [MessageEndpoint(CommandEndpoint)]
    private record OwnEndpointCommand(Guid CorrelationId) : ICommand;

    private record DefaultEndpointInstanceDependentCommand(Guid CorrelationId) : IInstanceDependentCommand;

    [MessageEndpoint(CommandEndpoint)]
    private record OwnEndpointInstanceDependentCommand(Guid CorrelationId) : IInstanceDependentCommand;

    private record ItemRequest : IRequest<ItemResponse>;

    [MessageEndpoint(RequestEndpoint)]
    private record ItemOwnEndpointRequest : IRequest<ItemResponse>;

    [MessageEndpoint(RequestEndpoint)]
    private record ItemOwnEndpointInstanceDependentRequest : IInstanceDependentRequest<ItemResponse>;

    private record ItemDefaultEndpointInstanceDependentRequest : IInstanceDependentRequest<ItemResponse>;

    private record ItemResponse(ErrorInfo? RequestError) : IResponse;

    private record Something;

    [MessageEndpoint(ActivityEndpoint)]
    private record DataActivityOwnEndpointInstanceIndependent : IActivityArgument;

    [MessageEndpoint(ActivityEndpoint)]
    private record DataActivityOwnEndpointInstanceDependent : IActivityArgument, IInstanceDependentMessage;

    private class GenericSomething<T>
    {
        public List<T> MyList = [];
    }

    private class GenericCommand<T> : ICommand
    {
        public List<T> MyList = [];
        public Guid CorrelationId { get; init; } = Guid.NewGuid();
    }

    [MessageEndpoint($"{CommandEndpoint}_{nameof(OtherGenericCommand<T>)}")]
    private class OtherGenericCommand<T> : ICommand
    {
        public List<T> MyList = [];
        public Guid CorrelationId { get; init; } = Guid.NewGuid();
    }

    /// <summary>
    /// it's to consume instance dependent events and commands at the same time
    /// </summary>
    private class InvalidCommandAndEventConsumer : IConsumer<InstanceDependentEvent>, IConsumer<SomeCommand>
    {
        public Task Consume(ConsumeContext<InstanceDependentEvent> context)
            => Task.CompletedTask;

        public Task Consume(ConsumeContext<SomeCommand> context)
            => Task.CompletedTask;
    }

    private class InvalidCommandAndIndependentEventConsumer : IConsumer<InstanceIndependentEvent>, IConsumer<SomeCommand>
    {
        public Task Consume(ConsumeContext<InstanceIndependentEvent> context)
            => Task.CompletedTask;

        public Task Consume(ConsumeContext<SomeCommand> context)
            => Task.CompletedTask;
    }

    /// <summary>
    /// it is invalid to consume multiple events if at least one is instance dependent
    /// </summary>
    private class InvalidMultiEventConsumer : IConsumer<InstanceDependentEvent>, IConsumer<InstanceIndependentEvent>
    {
        public Task Consume(ConsumeContext<InstanceDependentEvent> context)
            => Task.CompletedTask;

        public Task Consume(ConsumeContext<InstanceIndependentEvent> context)
            => Task.CompletedTask;
    }

    /// <summary>
    /// it is invalid to consume multiple commands
    /// </summary>
    private class InvalidMultiCommandConsumer : IConsumer<SomeCommand>, IConsumer<SomeOtherCommand>
    {
        public Task Consume(ConsumeContext<SomeCommand> context)
            => Task.CompletedTask;

        public Task Consume(ConsumeContext<SomeOtherCommand> context)
            => Task.CompletedTask;
    }

    /// <summary>
    /// it is valid to consume multiple instance independent events
    /// </summary>
    private class ValidMultiEventConsumer : IConsumer<InstanceIndependentEvent>, IConsumer<OtherInstanceIndependentEvent>
    {
        public Task Consume(ConsumeContext<InstanceIndependentEvent> context)
            => Task.CompletedTask;

        public Task Consume(ConsumeContext<OtherInstanceIndependentEvent> context)
            => Task.CompletedTask;
    }

    private class ValidTrackingConsumer : TrackingConsumerBase, IConsumer<SomeCommand>
    {
        public Task Consume(ConsumeContext<SomeCommand> context)
            => Task.CompletedTask;

        protected override Task ConsumeCompleted(ConsumeContext<RoutingSlipCompleted> context)
            => Task.CompletedTask;

        protected override Task ConsumeFaulted(ConsumeContext<RoutingSlipFaulted> context)
            => Task.CompletedTask;
    }

    private class ValidSomethingConsumer : IConsumer<Something>
    {
        public Task Consume(ConsumeContext<Something> context)
            => Task.CompletedTask;
    }

    private class GenericSomethingConsumer<T> : IConsumer<Something>
    {
        public List<T> SomeFoo = [];

        public Task Consume(ConsumeContext<Something> context)
            => Task.CompletedTask;
    }

    private class ValidOwnEndpointCommandConsumer : IConsumer<OwnEndpointCommand>
    {
        public Task Consume(ConsumeContext<OwnEndpointCommand> context)
            => Task.CompletedTask;
    }

    private class ValidInstanceDependentEventConsumer : IConsumer<InstanceDependentEvent>
    {
        public Task Consume(ConsumeContext<InstanceDependentEvent> context)
            => Task.CompletedTask;
    }

    [ReadOnlyConsumer]
    private class ValidDefaultEndpointInstanceDependentCommandConsumer : IConsumer<DefaultEndpointInstanceDependentCommand>
    {
        public Task Consume(ConsumeContext<DefaultEndpointInstanceDependentCommand> context)
            => Task.CompletedTask;
    }

    [ReadOnlyConsumer]
    private class ValidOwnEndpointInstanceDependentCommandConsumer : IConsumer<OwnEndpointInstanceDependentCommand>
    {
        public Task Consume(ConsumeContext<OwnEndpointInstanceDependentCommand> context)
            => Task.CompletedTask;
    }

    private class InvalidOwnEndpointInstanceDependentCommandConsumer : IConsumer<OwnEndpointInstanceDependentCommand>
    {
        public Task Consume(ConsumeContext<OwnEndpointInstanceDependentCommand> context)
            => Task.CompletedTask;
    }

    private class ConsumerActivityOwnEndpointInstanceIndependent : IConsumer<DataActivityOwnEndpointInstanceIndependent>
    {
        public Task Consume(ConsumeContext<DataActivityOwnEndpointInstanceIndependent> context)
            => Task.CompletedTask;
    }

    private class ConsumerActivityOwnEndpointInstanceDependent : IConsumer<DataActivityOwnEndpointInstanceDependent>
    {
        public Task Consume(ConsumeContext<DataActivityOwnEndpointInstanceDependent> context)
            => Task.CompletedTask;
    }

    private class SystemCompensateActivity : ICompensateActivity<DataActivityOwnEndpointInstanceDependent>
    {
        public Task<CompensationResult> Compensate(CompensateContext<DataActivityOwnEndpointInstanceDependent> context)
        {
            var resultMock = Substitute.For<CompensationResult>();
            return Task.FromResult(resultMock);
        }
    }

    private class SystemExecuteActivity : IExecuteActivity<DataActivityOwnEndpointInstanceDependent>
    {
        public Task<ExecutionResult> Execute(ExecuteContext<DataActivityOwnEndpointInstanceDependent> context)
        {
            var resultMock = Substitute.For<ExecutionResult>();
            return Task.FromResult(resultMock);
        }
    }
}
