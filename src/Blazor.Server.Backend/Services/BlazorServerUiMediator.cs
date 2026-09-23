using System.Security.Principal;
using Blazor.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;

namespace Blazor.Server.Backend.Services;

public sealed partial class BlazorServerUiMediator(ISuiteMediator suiteMediator, IServiceProvider services) : IUiMediator
{
    private IIdentity? _scopeIdentity;

    public int CommandTimeoutMs => Shared.Constants.CommandTimeoutMs;

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
        TryScopeIdentityInitialization();

        try
        {
            var logger = services.GetRequiredService<ILogger<BlazorServerUiMediator>>();

            LogRegisterEvent(logger, handler.GetType().FullName, typeof(TEvent).FullName, _scopeIdentity?.Name);

            var subscriptionHolder = services.GetRequiredService<IUiEventSubscriptionHolder<TEvent>>();
            return subscriptionHolder.Connect(handler, _scopeIdentity);
        }
        catch (ObjectDisposedException)
        {
            // In case the scope has already been disposed, we return a dummy disposable.
            return new DummyDisposable();
        }
    }

    private class DummyDisposable : IDisposable
    {
        public void Dispose()
        {
            // No-op
        }
    }

    /// <summary>
    /// The service is scoped, so the scope's identity only has to be initialized once.
    /// </summary>
    private void TryScopeIdentityInitialization()
    {
        if (_scopeIdentity is not null)
            return;

        var httpContextAccessor = services.GetRequiredService<IHttpContextAccessor>();
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext?.User.Identity?.IsAuthenticated != true)
            return;

        _scopeIdentity = httpContext?.User.Identity!;
    }

    [LoggerMessage(LogLevel.Trace, "UI register consumer={Consumer} event={Event} user={User}")]
    private static partial void LogRegisterEvent(ILogger<BlazorServerUiMediator> logger, string? Consumer, string? Event, string? User);
}
