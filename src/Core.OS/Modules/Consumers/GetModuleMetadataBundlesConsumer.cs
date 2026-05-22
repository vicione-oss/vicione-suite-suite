using Core.OS.Modules.Contracts;
using Core.Shared.Modules;
using Core.Shared.Modules.Requests;
using Sdk.Backend.Messaging;

namespace Core.OS.Modules.Consumers;

public sealed partial class GetModuleMetadataBundlesConsumer(IModuleMetadataProvider metadataProvider, ILogger<GetModuleMetadataBundlesConsumer> logger)
    : RequestConsumer<GetModuleMetadataBundlesRequest, GetModuleMetadataBundlesResponse>
{
    public override async Task<GetModuleMetadataBundlesResponse> Respond(GetModuleMetadataBundlesRequest message, CancellationToken cancellationToken)
    {
        var options = new GetModuleMetadataOptions(message.Installed, message.Available, message.ForceRefresh);

        var bundles = await metadataProvider.GetModuleMetadata(options, cancellationToken);

        return new GetModuleMetadataBundlesResponse(bundles);
    }

    public override Task<GetModuleMetadataBundlesResponse> HandleException(GetModuleMetadataBundlesRequest message, Exception e, CancellationToken cancellationToken)
    {
        LogError(logger, e);

        // if we failed to request available modules we still can deliver installed ones        
        return Task.FromResult(new GetModuleMetadataBundlesResponse([], new(ModuleErrorCodes.RequestVersionsFailed, e.Message)));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to get module metadata bundles")]
    private static partial void LogError(ILogger<GetModuleMetadataBundlesConsumer> logger, Exception exception);
}
