using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Core.OS.HostManagement.Mappers;
using Core.OS.Instance;
using HostManagement.Shared.Communication;
using HostManagement.Shared.Communication.Contracts;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.System;
using CommunicationJsonContext = HostManagement.Shared.Communication.Contracts.SourceGenerationContext;
using SharedJsonContext = HostManagement.Shared.Contracts.SourceGenerationContext;

namespace Core.OS.HostManagement.Extensions;

internal static class IPipeClientExtensions
{
    public static async Task<InstallSignedDebianPackageResult?> InstallSignedDebianPackage(this IPipeClient pipeClient, SignedDebianPackage debianPackage, CancellationToken cancellationToken = default)
    {
        var requestJson = JsonSerializer.Serialize(debianPackage, SharedJsonContext.Default.SignedDebianPackage);

        return await GetRequestResult(
                () => pipeClient.SendRequest(Topics.InstallSignedDebianPackage, requestJson, cancellationToken),
                CommunicationJsonContext.Default.InstallSignedDebianPackageResult);
    }

    public static Task<SetSystemConfigurationResult?> SetMappedSystemConfiguration(this IPipeClient pipeClient, Sdk.SystemConfiguration.Contracts.SystemConfiguration systemConfiguration, CancellationToken cancellationToken = default)
    {
        var mapper = new SystemConfigurationMapper();
        var hmConfig = mapper.ToHostManagementFormat(systemConfiguration);

        return pipeClient.SetSystemConfiguration(hmConfig, cancellationToken);
    }

    public static async Task<SetSystemConfigurationResult?> SetSystemConfiguration(this IPipeClient pipeClient, SystemConfiguration systemConfiguration, CancellationToken cancellationToken = default)
    {
        var configJson = JsonSerializer.Serialize(systemConfiguration, CommunicationJsonContext.Default.SystemConfiguration);

        return await GetRequestResult(
            () => pipeClient.SendRequest(Topics.SetSystemConfiguration, configJson, cancellationToken),
            CommunicationJsonContext.Default.SetSystemConfigurationResult);
    }

    public static async Task<GetSystemConfigurationResult?> GetSystemConfiguration(this IPipeClient pipeClient, CancellationToken cancellationToken = default)
        => await GetRequestResult(
            () => pipeClient.SendRequest(Topics.GetSystemConfiguration, string.Empty, cancellationToken),
            CommunicationJsonContext.Default.GetSystemConfigurationResult);

    public static async Task<GetDHCPLeaseInformationResult?> GetDHCPLeaseInformation(this IPipeClient pipeClient, string networkInterfaceName, CancellationToken cancellationToken = default)
        => await GetRequestResult(
            () => pipeClient.SendRequest(Topics.GetDHCPLeaseInformation, networkInterfaceName, cancellationToken),
            CommunicationJsonContext.Default.GetDHCPLeaseInformationResult);

    public static async Task<GetNetworkStatusInformationResult?> GetNetworkStatusInformation(this IPipeClient pipeClient, string networkInterfaceName, CancellationToken cancellationToken = default)
        => await GetRequestResult(
            () => pipeClient.SendRequest(Topics.GetNetworkStatusInformation, networkInterfaceName, cancellationToken),
            CommunicationJsonContext.Default.GetNetworkStatusInformationResult);

    public static async Task<GetNTPFallbackInformationResult?> GetNTPFallbackInformation(this IPipeClient pipeClient, CancellationToken cancellationToken = default)
        => await GetRequestResult(
            () => pipeClient.SendRequest(Topics.GetNTPFallbackInformation, string.Empty, cancellationToken),
            CommunicationJsonContext.Default.GetNTPFallbackInformationResult);

    public static async Task<GetOriginalPhysicalAddressResult?> GetOriginalPhysicalAddress(this IPipeClient pipeClient, string networkInterfaceName, CancellationToken cancellationToken = default)
        => await GetRequestResult(
            () => pipeClient.SendRequest(Topics.GetOriginalPhysicalAddress, networkInterfaceName, cancellationToken),
            CommunicationJsonContext.Default.GetOriginalPhysicalAddressResult);

#pragma warning disable CS0618 // Type or member is obsolete but still in use and not replaced by GetDHCPLeaseInformationResult yet
    public static async Task<RenewDHCPLeaseResult?> RenewDHCPLease(this IPipeClient pipeClient, string networkInterfaceName, CancellationToken cancellationToken = default)
#pragma warning restore CS0618 // Type or member is obsolete
        => await GetRequestResult(
            () => pipeClient.SendRequest(Topics.RenewDHCPLease, networkInterfaceName, cancellationToken),
            CommunicationJsonContext.Default.RenewDHCPLeaseResult);

    public static async Task<UpdateSystemResult?> UpdateSystem(this IPipeClient pipeClient, string filePath, CancellationToken cancellationToken = default)
        => await GetRequestResult(
            () => pipeClient.SendRequest(Topics.UpdateSystem, filePath, cancellationToken),
            CommunicationJsonContext.Default.UpdateSystemResult);

    public static async Task<ResetSystemResult?> ResetSystem(this IPipeClient pipeClient, CancellationToken cancellationToken = default)
        => await GetRequestResult(
            () => pipeClient.SendRequest(Topics.ResetSystem, string.Empty, cancellationToken),
            CommunicationJsonContext.Default.ResetSystemResult);

    public static async Task<RestartSystemResult?> RestartSystem(this IPipeClient pipeClient, CancellationToken cancellationToken = default)
        => await GetRequestResult(
            () => pipeClient.SendRequest(Topics.RestartSystem, string.Empty, cancellationToken),
            CommunicationJsonContext.Default.RestartSystemResult);

    public static async Task<ServiceControlResult> RestartService(this IPipeClient pipeClient, string serviceName, CancellationToken cancellationToken = default)
        => (await GetRequestResult(
            () => pipeClient.SendRequest(Topics.RestartService, serviceName, cancellationToken),
            CommunicationJsonContext.Default.ServiceControlResult))
        ?? throw new InvalidOperationException($"Send restart service='{serviceName}' request returned null");

    public static Task<ServiceControlResult> RestartSuite(this IPipeClient pipeClient, InstanceOptions options, CancellationToken cancellationToken = default)
        => RestartService(pipeClient, options.ServiceName, cancellationToken);

    public static async Task<ShutdownSystemResult?> ShutdownSystem(this IPipeClient pipeClient, CancellationToken cancellationToken = default)
        => await GetRequestResult(
            () => pipeClient.SendRequest(Topics.ShutdownSystem, string.Empty, cancellationToken),
            CommunicationJsonContext.Default.ShutdownSystemResult);

    public static async Task<ServiceControlResult?> StartService(this IPipeClient pipeClient, string serviceName, CancellationToken cancellationToken = default)
        => await GetRequestResult(
            () => pipeClient.SendRequest(Topics.StartService, serviceName, cancellationToken),
            CommunicationJsonContext.Default.ServiceControlResult);

    public static async Task<ServiceControlResult?> StopService(this IPipeClient pipeClient, string serviceName, CancellationToken cancellationToken = default)
        => await GetRequestResult(
            () => pipeClient.SendRequest(Topics.StopService, serviceName, cancellationToken),
            CommunicationJsonContext.Default.ServiceControlResult);

    private static async Task<TResult?> GetRequestResult<TResult>(Func<Task<string>> pipeRequest, JsonTypeInfo<TResult> typeInfo)
        where TResult : class
    {
        // Maybe the topic is not supported and we got an error response
        // This will change with next HM 2.x version where we are able to check the supported topics/features
        var settingsJson = await pipeRequest();

        try
        {
            // Usually we get the expected result except the topic/feature is not enabled
            return JsonSerializer.Deserialize(settingsJson, typeInfo);
        }
        catch (JsonException jsonEx)
        {
            // Then we will get a default error response to get reason
            var error = JsonSerializer.Deserialize(settingsJson, CommunicationJsonContext.Default.Response);
            throw new InvalidOperationException($"Pipe request failed with error: {error?.Message}", jsonEx);
        }
    }
}
