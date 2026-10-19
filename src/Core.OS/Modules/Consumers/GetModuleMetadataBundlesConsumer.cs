using Core.Module.Options;
using Core.OS.Modules.Contracts;
using Core.Shared.Modules;
using Core.Shared.Modules.Requests;
using Microsoft.Extensions.Options;
using Sdk.Backend.Messaging;

namespace Core.OS.Modules.Consumers;

public sealed partial class GetModuleMetadataBundlesConsumer(IModuleMetadataProvider metadataProvider,
    IOptions<ModuleLoaderOptions> loaderOptions,
    ILogger<GetModuleMetadataBundlesConsumer> logger)
    : RequestConsumer<GetModuleMetadataBundlesRequest, GetModuleMetadataBundlesResponse>
{
    public override async Task<GetModuleMetadataBundlesResponse> Respond(GetModuleMetadataBundlesRequest message, CancellationToken cancellationToken)
    {
        var options = new GetModuleMetadataOptions(message.Installed, message.Available, message.ForceRefresh, message.ResolveUpdates);

        var bundles = await metadataProvider.GetModuleMetadata(options, cancellationToken);

        return new GetModuleMetadataBundlesResponse(bundles, AllowInstallation: loaderOptions.Value.AllowInstallation);
    }

    public override Task<GetModuleMetadataBundlesResponse> HandleException(GetModuleMetadataBundlesRequest message, Exception e, CancellationToken cancellationToken)
    {
        LogError(logger, e);

        // The caller gets the error and no bundles, but still learns whether installation is allowed.
        return Task.FromResult(new GetModuleMetadataBundlesResponse([], new(ModuleErrorCodes.RequestVersionsFailed, e.Message),
            loaderOptions.Value.AllowInstallation));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to get module metadata bundles")]
    private static partial void LogError(ILogger<GetModuleMetadataBundlesConsumer> logger, Exception exception);
}
