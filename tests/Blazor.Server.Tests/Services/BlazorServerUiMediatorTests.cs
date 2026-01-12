using Blazor.Server.Backend.Services;
using Blazor.Shared.Services;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Backend.Messaging;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Sdk.Testing.Backend;
using Xunit;

namespace Blazor.Server.Tests.Services;

public class BlazorServerUiMediatorTests
{
    private readonly ISuiteMediator _suiteMediator = Substitute.For<ISuiteMediator>();
    private readonly IUiEventSubscriptionHolder<FooEvent> _publisher = Substitute.For<IUiEventSubscriptionHolder<FooEvent>>();
    private IServiceProvider? _serviceProvider;

    [Fact]
    public async Task Sends_command()
    {
        // Arrange
        var uiMediator = SetupTest();
        var command = new BarTestCommand(Guid.NewGuid())
        {
            InstanceId = Guid.NewGuid(),
        };
        // Act
        await uiMediator.Send(command);

        // Assert
        await _suiteMediator.Received().Send(command, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sends_instance_dependent_command()
    {
        // Arrange
        var uiMediator = SetupTest();
        var command = new FooTestCommand
        {
            InstanceId = Guid.NewGuid(),
        };
        // Act
        await uiMediator.Send(command, command.InstanceId);

        // Assert
        await _suiteMediator.Received().Send(command, command.InstanceId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_response_message()
    {
        // Arrange
        var uiMediator = SetupTest();
        var fooTestRequest = new FooTestRequest(Guid.NewGuid());
        var fooTestResponse = new FooTestResponse()
        {
            ClusterJson = @" [ {""name"": ""John Doe"", ""occupation"": ""gardener""}, 
                               {""name"": ""Peter Novak"", ""occupation"": ""driver""} ]"
        };

        _suiteMediator.SetupRequest<FooTestRequest, FooTestResponse>(null, fooTestResponse);

        // Act
        var responseMessage = await uiMediator.Request<FooTestRequest, FooTestResponse>(fooTestRequest);

        // Assert
        responseMessage.ClusterJson.Should().Be(fooTestResponse.ClusterJson);
    }

    [Fact]
    public void Register_handler()
    {
        // Arrange
        var uiMediator = SetupTest();

        var fooHandler = new FooService();

        // Act
        _ = uiMediator.Register(fooHandler);

        // Assert
        _publisher.Received().Connect(fooHandler);
    }

    private BlazorServerUiMediator SetupTest()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_publisher);
        _serviceProvider = services.BuildServiceProvider();

        var mediator = new BlazorServerUiMediator(_suiteMediator, _serviceProvider);

        return mediator;
    }

    public record FooTestCommand : IInstanceDependentCommand
    {
        public Guid InstanceId { get; set; }
        public Guid CorrelationId => InstanceId;
    }

    public record BarTestCommand(Guid BarId) : ICommand
    {
        public Guid InstanceId { get; set; }

        public Guid CorrelationId => BarId;
    }

    public record FooTestRequest(Guid RequestId) : IRequest<FooTestResponse>;

    public record FooTestResponse : IResponse
    {
        public ErrorInfo? RequestError { get; init; }
        public string? ClusterJson { get; init; }
    }

    public sealed record FooEvent : IEvent;

    public class FooService : IEventConsumer<FooEvent>
    {
        public Task Consume(ClientContext<FooEvent> context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }
}
