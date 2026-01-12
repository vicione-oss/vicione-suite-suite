using Blazor.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Backend.Messaging;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;

namespace Blazor.Server.Backend.Services;

public sealed class BlazorServerUiMediator(ISuiteMediator suiteMediator, IServiceProvider services) : IUiMediator
{
    public Task Send<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : class, ICommand
        => suiteMediator.Send(command, cancellationToken);

    public Task Send<TCommand>(TCommand command, Guid instanceId, CancellationToken cancellationToken = default)
        where TCommand : class, IInstanceDependentCommand
        => suiteMediator.Send(command, instanceId, cancellationToken);

    public Task<TResponse> Request<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : class, IRequest<TResponse>
        where TResponse : class, IResponse
        => suiteMediator.Request<TRequest, TResponse>(request, cancellationToken);

    public Task<TResponse> Request<TRequest, TResponse>(TRequest request, Guid instanceId, CancellationToken cancellationToken = default)
        where TRequest : class, IInstanceDependentRequest<TResponse>
        where TResponse : class, IResponse
        => suiteMediator.Request<TRequest, TResponse>(request, instanceId, cancellationToken);

    public IDisposable Register<TEvent>(IEventConsumer<TEvent> handler)
        where TEvent : class, IEvent
    {
        var subscriptionHolder = services.GetRequiredService<IUiEventSubscriptionHolder<TEvent>>();
        return subscriptionHolder.Connect(handler);
    }
}
