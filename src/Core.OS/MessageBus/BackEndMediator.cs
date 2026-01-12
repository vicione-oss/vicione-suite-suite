using Core.OS.MessageBus.MassTransit;
using Core.Shared.Instance.HealthCheck;
using MassTransit;
using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace Core.OS.MessageBus;

internal sealed class BackEndMediator(IPublishEndpoint publishEndpoint,
    ISendEndpointProvider sendEndpointProvider,
    IScopedClientFactory clientFactory,
    IMasterHealthInfo masterHealthInfo,
    IServiceProvider services) : ISuiteMediator
{
    private readonly ILocalBus? _localBus = services.GetService<ILocalBus>();

    public async Task Send<TCommand>(TCommand message, CancellationToken cancellationToken)
        where TCommand : class, ICommand
    {
        if (masterHealthInfo.IsMasterReachable)
        {
            var endPoint = await sendEndpointProvider.GetSendEndpoint(MessagingHelper.GetCommandEndpointAddress<TCommand>());
            await endPoint.Send(message, cancellationToken);
        }
    }

    public async Task Send<TCommand>(TCommand message, Guid instanceId, CancellationToken cancellationToken = default)
        where TCommand : class, IInstanceDependentCommand
    {
        if (masterHealthInfo.IsMasterReachable)
        {
            var endPoint = await sendEndpointProvider.GetSendEndpoint(MessagingHelper.GetCommandEndpointAddress<TCommand>(instanceId));
            await endPoint.Send(message, cancellationToken);
        }
    }

    public async Task Publish<T>(T message, CancellationToken cancellationToken = default)
        where T : class, IEvent
        => await publishEndpoint.Publish(message, cancellationToken);

    public async Task<TResponse> Request<TRequest, TResponse>(TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class, IRequest<TResponse>
        where TResponse : class, IResponse
    {
        var handle = GetRequestHandle<TRequest, TResponse>(request, null, cancellationToken);
        var response = await handle.GetResponse<TResponse>();
        return response.Message;
    }

    public async Task<TResponse> Request<TRequest, TResponse>(TRequest request,
        TimeSpan timeOut,
        CancellationToken cancellationToken = default)
        where TRequest : class, IRequest<TResponse>
        where TResponse : class, IResponse
    {
        var handle = GetRequestHandle<TRequest, TResponse>(request, timeOut, cancellationToken);
        var response = await handle.GetResponse<TResponse>();
        return response.Message;
    }

    public async Task<TResponse> Request<TRequest, TResponse>(TRequest request,
        Guid instanceId,
        CancellationToken cancellationToken = default)
        where TRequest : class, IInstanceDependentRequest<TResponse>
        where TResponse : class, IResponse
    {
        var handle = GetRequestHandle<TRequest, TResponse>(request, instanceId, null, cancellationToken);
        var response = await handle.GetResponse<TResponse>();
        return response.Message;
    }

    public async Task<TResponse> Request<TRequest, TResponse>(TRequest request,
        Guid instanceId,
        TimeSpan timeOut,
        CancellationToken cancellationToken = default)
        where TRequest : class, IInstanceDependentRequest<TResponse>
        where TResponse : class, IResponse
    {
        var handle = GetRequestHandle<TRequest, TResponse>(request, instanceId, timeOut, cancellationToken);
        var response = await handle.GetResponse<TResponse>();
        return response.Message;
    }

    private RequestHandle<TRequest> GetRequestHandle<TRequest, TResponse>(TRequest request,
        TimeSpan? timeOut,
        CancellationToken cancellationToken)
        where TRequest : class, IRequest<TResponse>
        where TResponse : class, IResponse
    {
        var requestTimeout = timeOut is null ? RequestTimeout.Default : RequestTimeout.After(ms: (int)timeOut.Value.TotalMilliseconds);
        var endpointAddress = MessagingHelper.GetRequestEndpointAddress<TRequest, TResponse>();
        return _localBus is not null
            ? _localBus.CreateClientFactory().CreateRequest(endpointAddress, request, cancellationToken, requestTimeout)
            : clientFactory.CreateRequest(endpointAddress, request, cancellationToken, requestTimeout);
    }

    private RequestHandle<TRequest> GetRequestHandle<TRequest, TResponse>(TRequest request,
        Guid instanceId,
        TimeSpan? timeOut,
        CancellationToken cancellationToken)
        where TRequest : class, IInstanceDependentRequest<TResponse>
        where TResponse : class, IResponse
    {
        var requestTimeout = timeOut is null ? RequestTimeout.Default : RequestTimeout.After(ms: (int)timeOut.Value.TotalMilliseconds);
        var endpointAddress = MessagingHelper.GetRequestEndpointAddress<TRequest, TResponse>(instanceId);
        return clientFactory.CreateRequest(endpointAddress, request, cancellationToken, requestTimeout);
    }
}
