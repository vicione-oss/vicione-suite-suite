using System.Text.Json;
using Core.OS.HostManagement.Mappers;
using HostManagement.Shared.Communication;
using HostManagement.Shared.Communication.Contracts;
using HostManagement.Shared.Contracts;
using CommunicationJsonContext = HostManagement.Shared.Communication.Contracts.SourceGenerationContext;
using SourceGenerationContext = HostManagement.Shared.Contracts.SourceGenerationContext;

namespace Core.OS.HostManagement.Extensions;

internal static class IPipeClientExtensions
{
    public static Task<SetSystemConfigurationResult?> SetMappedSystemConfiguration(this IPipeClient pipeClient, Sdk.SystemConfiguration.Contracts.SystemConfiguration systemConfiguration, CancellationToken cancellationToken = default)
    {
        var mapper = new SystemConfigurationMapper();
        var hmConfig = mapper.ToHostManagementFormat(systemConfiguration);

        return pipeClient.SetSystemConfiguration(hmConfig, cancellationToken);
    }

    public static async Task<SetSystemConfigurationResult?> SetSystemConfiguration(this IPipeClient pipeClient, SystemConfiguration systemConfiguration, CancellationToken cancellationToken = default)
    {
        var configJson = JsonSerializer.Serialize(systemConfiguration, SourceGenerationContext.Default.SystemConfiguration);
        var resultJson = await pipeClient.SendRequest(Topics.SetSystemConfiguration, configJson, cancellationToken);
        return JsonSerializer.Deserialize(resultJson, CommunicationJsonContext.Default.SetSystemConfigurationResult);
    }

    public static async Task<ServiceControlResult?> SendServiceControlTopic(this IPipeClient pipeClient, string topic, string serviceName, CancellationToken cancellationToken)
    {
        var resultJson = await pipeClient.SendRequest(topic, serviceName, cancellationToken);
        return JsonSerializer.Deserialize(resultJson, CommunicationJsonContext.Default.ServiceControlResult);
    }

    public static async Task<ResetSystemResult?> SendResetSystem(this IPipeClient pipeClient, CancellationToken cancellationToken = default)
    {
        var resultJson = await pipeClient.SendRequest(Topics.ResetSystem, string.Empty, cancellationToken);
        return JsonSerializer.Deserialize(resultJson, CommunicationJsonContext.Default.ResetSystemResult);
    }

    public static async Task<RestartSystemResult?> SendRestartSystem(this IPipeClient pipeClient, CancellationToken cancellationToken = default)
    {
        var resultJson = await pipeClient.SendRequest(Topics.RestartSystem, string.Empty, cancellationToken);
        return JsonSerializer.Deserialize(resultJson, CommunicationJsonContext.Default.RestartSystemResult);
    }
}
