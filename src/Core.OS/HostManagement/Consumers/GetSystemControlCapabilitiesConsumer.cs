using Core.OS.HostManagement.Extensions;
using Core.Shared.HostManagement.Requests;
using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace Core.OS.HostManagement.Consumers;

public sealed class GetSystemControlCapabilitiesConsumer(IPipeClient pipeClient, ILogger<GetSystemControlCapabilitiesConsumer> logger)
    : RequestConsumer<GetSystemControlCapabilities, GetSystemControlCapabilitiesResponse>
{
    public override async Task<GetSystemControlCapabilitiesResponse> Respond(GetSystemControlCapabilities message, CancellationToken cancellationToken)
    {
        var capabilities = await pipeClient.GetSupportedCapabilitiesOrNull(logger, cancellationToken);

        return new GetSystemControlCapabilitiesResponse
        {
            Capabilities = capabilities is null
                ? null
                : new SystemControlCapabilities(capabilities.Topics.RestartSystem, capabilities.Topics.RestartService, capabilities.Topics.ShutdownSystem)
        };
    }

    public override Task<GetSystemControlCapabilitiesResponse> HandleException(GetSystemControlCapabilities message, Exception e, CancellationToken cancellationToken)
        => Task.FromResult(new GetSystemControlCapabilitiesResponse { RequestError = new ErrorInfo(0, e.Message) });
}
