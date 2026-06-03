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

    private async Task<TResponse> HandleInstanceRequest<TRequest, TResponse>(TRequest request,
        Guid instanceId,
        TimeSpan? timeOut,
        CancellationToken cancellationToken = default)
        where TRequest : class, IInstanceDependentRequest<TResponse>
        where TResponse : class, IResponse
    {
        var localInstanceId = services.GetRequiredService<IInstanceInformationProvider>().Local.Id;
        if (instanceId == localInstanceId)
        {
            var localResponse = await TryInvokeInScopeAsync<InstanceDependentRequestConsumer<TRequest, TResponse>, TResponse>(
                (c, ct) => c.Respond(request, ct),
                (c, ex, ct) => c.HandleException(request, ex, ct),
                cancellationToken);
            if (localResponse is not null)
                return localResponse;
        }

        var handle = GetRequestHandle<TRequest, TResponse>(request, instanceId, timeOut, cancellationToken);
        return await GetInstanceResponse<TRequest, TResponse>(handle);
    }

    private async Task<TResponse> HandleLocalRequest<TRequest, TResponse>(TRequest request,
        TimeSpan? timeOut,
        CancellationToken cancellationToken = default)
        where TRequest : class, IRequest<TResponse>
        where TResponse : class, IResponse
    {
        var localResponse = await TryInvokeInScopeAsync<RequestConsumer<TRequest, TResponse>, TResponse>(
            (c, ct) => c.Respond(request, ct),
            (c, ex, ct) => c.HandleException(request, ex, ct),
            cancellationToken);
        if (localResponse is not null)
            return localResponse;

        var handle = GetRequestHandle<TRequest, TResponse>(request, timeOut, cancellationToken);
        return await GetResponse<TRequest, TResponse>(handle);
    }

    /// <summary>
    /// Resolves a locally registered consumer of type <typeparamref name="TConsumer"/> from a
    /// freshly created DI scope and invokes it. Returns <c>null</c> when no such consumer is
    /// registered (caller should then fall back to the message bus).
    /// </summary>
    /// <remarks>
    /// A new scope is mandatory here: the local request short-circuit (see
    /// <see cref="HandleLocalRequest{TRequest,TResponse}"/> /
    /// <see cref="HandleInstanceRequest{TRequest,TResponse}"/>) would otherwise resolve the consumer
    /// and all of its scoped dependencies (<c>DbContext</c>, <c>RoleManager</c>, <c>UserManager</c>, ...)
    /// from the caller's scope. In Blazor Server that scope is the long-lived circuit scope, so the
    /// same <c>DbContext</c> instance would be reused for every request in the user's session.
    /// <para>
    /// EF Core's default <c>QueryTrackingBehavior.TrackAll</c> performs identity resolution:
    /// when a query returns a row whose key is already in the <c>ChangeTracker</c>, the existing
    /// tracked CLR instance is returned and the freshly read column values are discarded. Combined
    /// with writes performed by command consumers (which DO run in fresh MassTransit-managed scopes
    /// via <c>UseMessageScope</c>), this causes the well-known "DB has the new value but the read
    /// still returns the old one" symptom. Creating a per-call scope here guarantees a fresh
    /// <c>DbContext</c> and brings the in-process path to parity with the bus-based path.
    /// </para>
    /// </remarks>
    private async Task<TResponse?> TryInvokeInScopeAsync<TConsumer, TResponse>(
        Func<TConsumer, CancellationToken, Task<TResponse>> respond,
        Func<TConsumer, Exception, CancellationToken, Task<TResponse>> handleException,
        CancellationToken cancellationToken)
        where TConsumer : class
        where TResponse : class
    {
        await using var scope = services.CreateAsyncScope();

        var consumer = scope.ServiceProvider.GetService<TConsumer>();
        if (consumer is null)
            return null;

        try
        {
            return await respond(consumer, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return await handleException(consumer, e, cancellationToken);
        }
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
            throw; //when we have discriminated unions, we could return a "TaskCanceledResponse" instead
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
            throw; //when we have discriminated unions, we could return a "TaskCanceledResponse" instead
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Task of request '{RequestName}' for response '{ResponseName}' was cancelled.")]
    private static partial void LogTaskCancelled(ILogger logger, string requestName, string responseName);
}
