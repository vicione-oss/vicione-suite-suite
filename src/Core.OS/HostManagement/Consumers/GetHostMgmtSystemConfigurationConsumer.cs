using System.Text.Json;
using Core.Shared.HostManagement;
using HostManagement.Shared.Communication;
using HostManagement.Shared.Communication.Enums;
using MassTransit;
using Sdk.Messaging;
using SourceGenerationContext = HostManagement.Shared.Communication.Contracts.SourceGenerationContext;

namespace Core.OS.HostManagement.Consumers;

public sealed class GetHostMgmtSystemConfigurationConsumer(IPipeClient pipeClient, SystemConfigurationCache responseCache) : RequestConsumer<GetHostMgmtSystemConfiguration, GetHostMgmtSystemConfigurationResponse>
{
    protected override Task<GetHostMgmtSystemConfigurationResponse> Respond(ConsumeContext<GetHostMgmtSystemConfiguration> context)
        => FetchSystemConfigurationFromHostManagement(pipeClient, responseCache, context.CancellationToken);

    protected override Task<GetHostMgmtSystemConfigurationResponse> HandleException(ConsumeContext<GetHostMgmtSystemConfiguration> context,
        Exception e)
        => Task.FromResult(new GetHostMgmtSystemConfigurationResponse { RequestError = new ErrorInfo(0, e.Message) });

    internal static async Task<GetHostMgmtSystemConfigurationResponse> FetchSystemConfigurationFromHostManagement(IPipeClient pipeClient, SystemConfigurationCache responseCache, CancellationToken cancellationToken = default)
    {
        var config = responseCache.Get();
        if (config is not null)
            return new GetHostMgmtSystemConfigurationResponse { Configuration = config };

        var settingsJson = await pipeClient.SendRequest(Topics.GetSystemConfiguration, "", cancellationToken);
        var configurationResult = JsonSerializer.Deserialize(settingsJson, SourceGenerationContext.Default.GetSystemConfigurationResult);

        if (configurationResult is { Status: OperationStatus.Success, Configuration: not null })
        {
            responseCache.Set(configurationResult.Configuration);

            return new GetHostMgmtSystemConfigurationResponse { Configuration = configurationResult.Configuration };
        }

        return new GetHostMgmtSystemConfigurationResponse
        {
            Configuration = configurationResult?.Configuration,
            RequestError = new ErrorInfo(0, configurationResult?.Message ?? "Could not fetch configuration from HostManagement")
        };
    }
}
