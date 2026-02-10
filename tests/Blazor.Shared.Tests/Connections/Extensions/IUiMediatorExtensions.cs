using Blazor.Shared.Connections.Services;
using NSubstitute;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Xunit;

namespace Blazor.Shared.Tests.Connections.Extensions;

internal static class IUiMediatorExtensions
{
    public static void Setup<TCommand, TEvent>(this IUiMediator uiMediator,
        Func<TCommand, TEvent> eventFactory, Func<ISuiteConnectionService?> suiteConnectionAccessor)
            where TCommand : class, ICommand
            where TEvent : class, IEvent
        => uiMediator.When(m => m.Send(Arg.Any<TCommand>(), Arg.Any<CancellationToken>()))
            .Do(async callinfo =>
            {
                var command = callinfo.Arg<TCommand>();
                var correlationId = command.CorrelationId;

                var @event = eventFactory(command);
                var context = new ClientContext<TEvent>(@event, correlationId);

                var suiteConnection = suiteConnectionAccessor();
                if (suiteConnection is not null)
                {
                    await suiteConnection.Initialize();

                    var eventConsumer = suiteConnection as IEventConsumer<TEvent>;
                    if (eventConsumer is not null)
                        await eventConsumer.Consume(context, TestContext.Current.CancellationToken);
                }
            });
}
