using Blazor.Wasm.Client.Infrastructure.SignalR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Blazor.Wasm.Tests.Client.Infrastructure.SignalR;

public sealed class ClientMessageHubTests : IAsyncDisposable
{
    private readonly NavigationManager _navigationMananger = new MockNavigationManager();
    private readonly ClientMessageHub _messageHub;

    public ClientMessageHubTests()
    {
        var loggerMock = Substitute.For<ILogger<ClientMessageHub>>();

        _messageHub = new ClientMessageHub(_navigationMananger, loggerMock);

        // todo: hub connection is created via static factory method - how to fake it?
    }

    public ValueTask DisposeAsync() => _messageHub.DisposeAsync();

    [Fact(Skip = "To be implemented...")]
    public void Tests_should_cover_all_message_hub_calls()
    {
        // Arrange

        // Act

        // Assert
    }

    private sealed class MockNavigationManager
        : NavigationManager
    {
        public bool WasNavigateInvoked { get; private set; }

        public MockNavigationManager() =>
            Initialize("http://localhost:2112/", "http://localhost:2112/test");

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            if (uri == "/")
            {
                Uri = BaseUri;
                NotifyLocationChanged(false);
            }
            WasNavigateInvoked = true;
        }
    }
}
