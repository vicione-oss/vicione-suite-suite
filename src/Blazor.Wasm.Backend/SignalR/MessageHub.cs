using System.Reflection;
using Blazor.Wasm.Client.Infrastructure.SignalR;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace Blazor.Wasm.Backend.SignalR;

public sealed class MessageHub(IHubContext<MessageHub> hubContext, IServiceProvider services, ILogger<MessageHub> logger) :
    Hub<IMessageHubClient>, IMessageHub
{
    public Task SendEvent(SignalRMessageEnvelope envelope)
        => hubContext.Clients.All.SendAsync(nameof(IMessageHubClient.ReceiveEvent), envelope);

    public async Task SendCommand(SignalRMessageEnvelope envelope)
    {
        logger.LogDebug("Received command:{Name} source:{Source} correlationId:{Id} over signalR",
            envelope.PayloadTypeFullName, envelope.SourceAddress, envelope.CorrelationId);

        var cmd = envelope.DeserializePayload(out var cmdType);

        var busControl = services.GetRequiredService<IBusControl>();

        // this happened on deploy on ec20-4 where ui commands arrived before the bus was ready!?
        var health = busControl.CheckHealth();
        if (health.Status != BusHealthStatus.Healthy)
        {
            logger.LogWarning("SendCommand skipped because bus is {State}", health.Status);
            return;
        }

        var endPoint =
            await busControl.GetSendEndpoint(
                MessagingHelper.GetCommandEndpointAddress(cmdType, envelope.InstanceId));

        await endPoint.Send(cmd, cmdType, context =>
        {
            context.CorrelationId = envelope.CorrelationId;
            // how to pass the source address from signalR client?
        }, CancellationToken.None);
    }

    public async Task<SignalRMessageEnvelope> SendRequest(SignalRMessageEnvelope envelope)
    {
        logger.LogDebug("Received request:{Name} source:{Source} correlationId:{Id} over signalR",
            envelope.PayloadTypeFullName, envelope.SourceAddress, envelope.CorrelationId);

        var request = envelope.DeserializePayload(out var requestType);

        // check ResponseType for ResponseType
        var responseType = GetResponseType(requestType);
        if (responseType is null)
            throw new InvalidOperationException(InterfaceNotFound(typeof(IRequest<>).Name, requestType.Name, requestType.Namespace));

        var mediator = services.GetRequiredService<ISuiteMediator>();

        var genericGetRequestMethod = GetGenericGetRequestMethod(requestType, responseType);

        // execute the GetResponse task
        try
        {
            dynamic result = genericGetRequestMethod.Invoke(mediator, [request, CancellationToken.None])
                ?? throw new InvalidOperationException("Failed to call generic get request method");

            var response = await result;

            // wrap to send it back
            return SignalRMessageFactory.EnvelopResponse(response, envelope.CorrelationId);
        }
        catch (RequestFaultException)
        {
            return new SignalRMessageEnvelope(responseType.FullName, "{}")
            {
                CorrelationId = envelope.CorrelationId,
                Failed = true
            };
        }
    }

    internal static MethodInfo GetGenericGetRequestMethod(Type requestType, Type responseType)
        => typeof(ISuiteMediator)
               .GetMethods()
               .FirstOrDefault(k =>
                   k.Name == nameof(ISuiteMediator.Request)) // better check also the parameters to get the right one!
               ?.MakeGenericMethod(requestType, responseType)
           ?? throw new MissingMethodException(nameof(ISuiteMediator), nameof(ISuiteMediator.Request));

    private static Type? GetResponseType(Type requestType)
        => requestType.GetInterfaces()
            .FirstOrDefault(i => i.GetGenericTypeDefinition().Name == typeof(IRequest<>).Name)?
            .GetGenericArguments()[0];

    private static string InterfaceNotFound(string attribute, string requestTypeName, string? requestTypeNamespace) =>
        $"Could not find {attribute} for '{requestTypeName}' in namespace '{requestTypeNamespace}'. cannot be executed";
}
