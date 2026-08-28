using Core.Shared.EnvironmentOverrides.Requests;
using Sdk.Backend.Messaging;

namespace Core.OS.EnvironmentOverrides.Consumers;

public sealed partial class GetEnvironmentOverridesConsumer(IEnvironmentOverridesRepository repository, ILogger<GetEnvironmentOverridesConsumer> logger)
    : InstanceDependentRequestConsumer<GetEnvironmentOverrides, GetEnvironmentOverridesResponse>
{
    public override async Task<GetEnvironmentOverridesResponse> Respond(GetEnvironmentOverrides message, CancellationToken cancellationToken)
    {
        var overrides = await repository.Get(cancellationToken);

        return new GetEnvironmentOverridesResponse(overrides.ToDictionary(StringComparer.Ordinal));
    }

    public override Task<GetEnvironmentOverridesResponse> HandleException(GetEnvironmentOverrides message, Exception e, CancellationToken cancellationToken)
    {
        LogFailedToHandleGetEnvironmentOverrides(logger, e);

        return Task.FromResult(
            new GetEnvironmentOverridesResponse(new Dictionary<string, string>(StringComparer.Ordinal), new(0, e.Message)));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to handle GetEnvironmentOverrides")]
    private static partial void LogFailedToHandleGetEnvironmentOverrides(ILogger<GetEnvironmentOverridesConsumer> logger, Exception exception);
}
