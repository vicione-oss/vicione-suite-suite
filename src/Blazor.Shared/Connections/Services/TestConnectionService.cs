using Core.Shared.Connections.Commands;
using Core.Shared.Connections.Contracts;
using Core.Shared.Connections.Events;
using Sdk.Client.Infrastructure;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Connections.Services;

public sealed class TestConnectionService : IEventConsumer<TestConnectionDoneEvent>, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly IUiMediator _mediator;
    private readonly Guid _requestId = Guid.NewGuid();
    private readonly IDisposable _resultSubscription;

    public CancellationToken? Token { get; private set; }

    public event Func<Guid, Task>? TestStarted;
    public event Func<TestConnectionResult, Task>? TestResultReceived;

    public TestConnectionService(IUiMediator mediator)
    {
        _mediator = mediator;
        _resultSubscription = _mediator.Register(this);
    }

    public Task Consume(ClientContext<TestConnectionDoneEvent> context, CancellationToken cancellationToken)
    {
        if (_requestId != context.Message.RequestId)
            return Task.CompletedTask;

        Token = null;

        return TestResultReceived is not null
            ? TestResultReceived.Invoke(context.Message.TestResult)
            : Task.CompletedTask;
    }

    public async Task TestConnection(Connection connection)
    {
        Token = _cancellationTokenSource.Token;

        if (TestStarted is not null)
            await TestStarted.Invoke(connection.Id);

        await _mediator.Send(new TestConnection(_requestId, connection), Token.Value);
    }

    public void Dispose()
    {
        _resultSubscription.Dispose();
        _cancellationTokenSource.Dispose();
    }

    public void Reset()
    {
        Token = null;
    }
}
