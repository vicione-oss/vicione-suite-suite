using System.Text.Json;
using AutoFixture;
using Blazor.Wasm.Backend.SignalR;
using Blazor.Wasm.Client.Infrastructure.SignalR;
using AwesomeAssertions;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Backend.Messaging;
using Sdk.Connections.Commands;
using Sdk.Connections.Contracts;
using Sdk.Connections.Requests;
using Sdk.Testing.Backend;
using Xunit;

namespace Blazor.Wasm.Tests.Backend.SignalR;

public sealed class MessageHubTests : IDisposable
{
    private readonly ISuiteMediator _mediatorMock = Substitute.For<ISuiteMediator>();
    private readonly ISendEndpoint _sendEndpointMock = Substitute.For<ISendEndpoint>();
    private readonly ILogger<MessageHub> _loggerMock = Substitute.For<ILogger<MessageHub>>();
    private readonly IHubContext<MessageHub> _hubContextMock = Substitute.For<IHubContext<MessageHub>>();
    private readonly IBusControl _busControlMock = Substitute.For<IBusControl>();

    private readonly Fixture _fixture = new();

    private readonly MessageHub _messageHub;

    public MessageHubTests()
    {
        _busControlMock.CheckHealth()
            .Returns(BusHealthResult.Healthy("Test", new Dictionary<string, EndpointHealthResult>()));

        _busControlMock.GetSendEndpoint(Arg.Any<Uri>())
            .Returns(_sendEndpointMock);

        var services = new ServiceCollection();
        services.AddSingleton(_busControlMock);
        services.AddSingleton(_mediatorMock);

        _messageHub = new MessageHub(_hubContextMock, services.BuildServiceProvider(), _loggerMock);
    }

    public void Dispose() => _messageHub.Dispose();

    [Fact]
    public async Task Send_envelope_should_put_command_on_messagebus()
    {
        // Arrange
        var connection = _fixture.Create<Connection>();
        var command = new UpsertConnection(connection);
        var envelope = SignalRMessageFactory.Envelop(command);

        // Act
        await _messageHub.SendCommand(envelope);

        // Assert
        _ = _sendEndpointMock.Received()
            .Send(Arg.Any<UpsertConnection>(), command.GetType(), Arg.Any<IPipe<SendContext>>(), CancellationToken.None);
    }

    [Fact]
    public async Task Send_request_should_return_response()
    {
        // Arrange
        var connections = new List<Connection> { _fixture.Create<Connection>(), _fixture.Create<Connection>() };
        var request = new GetConnections(null, null);
        var connectionsResponse = new GetConnectionsResponse(connections);
        var envelope = SignalRMessageFactory.Envelop(request);

        _mediatorMock.SetupRequest(request, connectionsResponse);

        // Act
        var responseEnvelope = await _messageHub.SendRequest(envelope);
        var response = JsonSerializer.Deserialize<GetConnectionsResponse>(responseEnvelope.Payload);

        // Assert
        Assert.IsType<GetConnectionsResponse>(response);
        connectionsResponse.Connections.Should().BeEquivalentTo(response.Connections);
    }

    [Fact]
    public async Task Fault_on_request_should_return_envelope_failed()
    {
        // Arrange
        var request = new GetConnections(null, null);
        var envelope = SignalRMessageFactory.Envelop(request);

        _mediatorMock.SetupRequestFault<GetConnections, GetConnectionsResponse>(request);

        // Act
        var responseEnvelope = await _messageHub.SendRequest(envelope);

        // Assert
        Assert.True(responseEnvelope.Failed);
    }

    [Fact]
    public void Call_generic_request_method_should_not_throw()
    {
        // Arrange

        var request = new GetConnections(Guid.NewGuid(), null);

        var mediatorMock = Substitute.For<ISuiteMediator>();

        mediatorMock.Request<GetConnections, GetConnectionsResponse>(request, CancellationToken.None)
            .Returns(Task.FromResult(new GetConnectionsResponse([])));

        // Act
        var genericGetRequest = MessageHub.GetGenericGetRequestMethod(typeof(GetConnections), typeof(GetConnectionsResponse));
        var requestHandle = genericGetRequest.Invoke(mediatorMock, [request, CancellationToken.None]);

        // Assert
        Assert.IsAssignableFrom<Task<GetConnectionsResponse>>(requestHandle);
    }
}
