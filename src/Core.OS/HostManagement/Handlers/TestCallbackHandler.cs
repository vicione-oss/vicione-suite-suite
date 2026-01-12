
namespace Core.OS.HostManagement.Handlers;

// Can be moved to tests before going to prod
public class TestCallbackHandler(ILogger<TestCallbackHandler> logger) : ICallbackHandler
{
    private readonly ILogger<TestCallbackHandler> _logger = logger;

    public string Topic => "test";

    public Task Handle(string messageContent, CancellationToken cancellationToken)
    {
        _logger.LogDebug("{Message}", messageContent);
        return Task.CompletedTask;
    }
}
