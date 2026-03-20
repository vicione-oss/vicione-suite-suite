using Core.OS.MessageBus.MassTransit;
using Core.Shared.Instance.HealthCheck;
using MassTransit;
using Sdk.Backend.Messaging;
using Sdk.Instance;
using Sdk.Messaging;

namespace Core.OS.MessageBus;

internal sealed partial class BackEndMediator(IPublishEndpoint publishEndpoint,
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
        var endPoint = await sendEndpointProvider.GetSendEndpoint(MessagingHelper.GetCommandEndpointAddress<TCommand>(instanceId));
        await endPoint.Send(message, cancellationToken);
    }

    public async Task Publish<T>(T message, CancellationToken cancellationToken = default)
        where T : class, IEvent
        => await publishEndpoint.Publish(message, cancellationToken);

    public Task<TResponse> Request<TRequest, TResponse>(TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class, IRequest<TResponse>
        where TResponse : class, IResponse
        => HandleLocalRequest<TRequest, TResponse>(request, null, cancellationToken);

    public Task<TResponse> Request<TRequest, TResponse>(TRequest request,
        TimeSpan timeOut,
        CancellationToken cancellationToken = default)
        where TRequest : class, IRequest<TResponse>
        where TResponse : class, IResponse
        => HandleLocalRequest<TRequest, TResponse>(request, timeOut, cancellationToken);

    public Task<TResponse> Request<TRequest, TResponse>(TRequest request,
        Guid instanceId,
        CancellationToken cancellationToken = default)
        where TRequest : class, IInstanceDependentRequest<TResponse>
        where TResponse : class, IResponse
        => HandleInstanceRequest<TRequest, TResponse>(request, instanceId, null, cancellationToken);

    public Task<TResponse> Request<TRequest, TResponse>(TRequest request,
        Guid instanceId,
        TimeSpan timeOut,
        CancellationToken cancellationToken = default)
        where TRequest : class, IInstanceDependentRequest<TResponse>
        where TResponse : class, IResponse
        => HandleInstanceRequest<TRequest, TResponse>(request, instanceId, timeOut, cancellationToken);

    private Task<TResponse> HandleInstanceRequest<TRequest, TResponse>(TRequest request,
        Guid instanceId,
        TimeSpan? timeOut,
        CancellationToken cancellationToken = default)
        where TRequest : class, IInstanceDependentRequest<TResponse>
        where TResponse : class, IResponse
    {
        var localInstanceId = services.GetRequiredService<IInstanceInformationProvider>().Local.Id;
        if (instanceId == localInstanceId)
        {
            var consumer = services.GetService<IConsumer<TRequest>>();
            if (consumer is InstanceDependentRequestConsumer<TRequest, TResponse> requestConsumer)
            {
                try
                {
                    return requestConsumer.Respond(request, cancellationToken);
                }
                catch (Exception e)
                {
                    return requestConsumer.HandleException(request, e, cancellationToken);
                }
            }
        }
        var handle = GetRequestHandle<TRequest, TResponse>(request, instanceId, timeOut, cancellationToken);
        return GetInstanceResponse<TRequest, TResponse>(handle);
    }
    
    private Task<TResponse> HandleLocalRequest<TRequest, TResponse>(TRequest request,
        TimeSpan? timeOut,
        CancellationToken cancellationToken = default)
        where TRequest : class, IRequest<TResponse>
        where TResponse : class, IResponse
    {
        var consumer = services.GetService<IConsumer<TRequest>>();
        if (consumer is RequestConsumer<TRequest, TResponse> requestConsumer)
        {
            try
            {
                return requestConsumer.Respond(request, cancellationToken);
            }
            catch (Exception e)
            {
                
                return requestConsumer.HandleException(request, e, cancellationToken);
            }
        }
        var handle = GetRequestHandle<TRequest, TResponse>(request, timeOut, cancellationToken);
        return GetResponse<TRequest, TResponse>(handle);
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

    private async Task<TResponse> GetResponse<TRequest, TResponse>(RequestHandle<TRequest> handle)
        where TRequest : class, IRequest<TResponse>
        where TResponse : class, IResponse
    {
        try
        {
            var response = await handle.GetResponse<TResponse>();
            return response.Message;
        }
        catch (TaskCanceledException)
        {
            var logger = services.GetRequiredService<ILogger<BackEndMediator>>();
            LogTaskCancelled(logger, typeof(TRequest).Name, typeof(TResponse).Name);
            return default!;
        }
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

    private async Task<TResponse> GetInstanceResponse<TRequest, TResponse>(RequestHandle<TRequest> handle)
        where TRequest : class, IInstanceDependentRequest<TResponse>
        where TResponse : class, IResponse
    {
        try
        {
            var response = await handle.GetResponse<TResponse>();
            return response.Message;
        }
        catch (TaskCanceledException)
        {
            var logger = services.GetRequiredService<ILogger<BackEndMediator>>();
            LogTaskCancelled(logger, typeof(TRequest).Name, typeof(TResponse).Name);
            return default!;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Task of request '{RequestName}' for response '{ResponseName}' was cancelled.")]
    private static partial void LogTaskCancelled(ILogger logger, string requestName, string responseName);
}
