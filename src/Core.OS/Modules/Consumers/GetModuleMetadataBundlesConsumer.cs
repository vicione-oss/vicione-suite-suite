using Core.OS.Modules.Contracts;
using Core.Shared.Modules;
using Core.Shared.Modules.Requests;
using MassTransit;
using Sdk.Backend.Messaging;

namespace Core.OS.Modules.Consumers;

public sealed class GetModuleMetadataBundlesConsumer(IModuleMetadataProvider metadataProvider, ILogger<GetModuleMetadataBundlesConsumer> logger)
    : RequestConsumer<GetModuleMetadataBundlesRequest, GetModuleMetadataBundlesResponse>
{
    protected override async Task<GetModuleMetadataBundlesResponse> Respond(ConsumeContext<GetModuleMetadataBundlesRequest> context)
    {
        var options = new GetModuleMetadataOptions(context.Message.Installed, context.Message.Available, context.Message.ForceRefresh);

        var bundles = await metadataProvider.GetModuleMetadata(options, context.CancellationToken);

        return new GetModuleMetadataBundlesResponse(bundles);
    }

    protected override Task<GetModuleMetadataBundlesResponse> HandleException(ConsumeContext<GetModuleMetadataBundlesRequest> context, Exception e)
    {
        logger.LogError(e, $"Failed to handle {nameof(GetModuleMetadataBundlesRequest)}");

        // if we failed to request available modules we still can deliver installed ones        
        return Task.FromResult(new GetModuleMetadataBundlesResponse([], new(ModuleErrorCodes.RequestVersionsFailed, e.Message)));
    }
}
