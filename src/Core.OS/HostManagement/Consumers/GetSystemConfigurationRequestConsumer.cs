using System.Net.NetworkInformation;
using Core.OS.HostManagement.Extensions;
using Core.OS.HostManagement.Mappers;
using HostManagement.Shared.Communication.Enums;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Network;
using Sdk.Backend.Messaging;
using Sdk.Messaging;
using Sdk.SystemConfiguration.Requests;

namespace Core.OS.HostManagement.Consumers;

public sealed partial class GetSystemConfigurationRequestConsumer(IPipeClient pipeClient, SystemConfigurationCache responseCache, ILogger<GetSystemConfigurationRequestConsumer> logger)
    : RequestConsumer<GetSystemConfiguration, GetSystemConfigurationResponse>
{
    public override async Task<GetSystemConfigurationResponse> Respond(GetSystemConfiguration message, CancellationToken cancellationToken)
    {
        var config = responseCache.Get();
        if (config is null)
        {
            var configurationResult = await pipeClient.GetSystemConfiguration(cancellationToken);
            if (configurationResult == null)
            {
                LogDeserializeReturnedNull(logger);

                return new GetSystemConfigurationResponse { RequestError = new ErrorInfo(1, "Deserialization failed") };
            }

            if (configurationResult.Configuration is not null)
                responseCache.Set(configurationResult.Configuration);

            if (configurationResult.Status == OperationStatus.Error)
            {
                LogErrorStatusReturned(logger, configurationResult.Message);

                return new GetSystemConfigurationResponse { RequestError = new ErrorInfo(3, configurationResult.Message) };
            }

            if (configurationResult.Status == OperationStatus.Warning)
                LogWarningStatusReturned(logger, configurationResult.Message);

            config = configurationResult.Configuration;
        }

        var additionalData = await GetOrFetchAdditionalData(config, cancellationToken);

        return new GetSystemConfigurationResponse { Configuration = config.ToSuiteFormat(additionalData) };
    }

    private async Task<SystemConfigurationAdditionalData> GetOrFetchAdditionalData(SystemConfiguration? config, CancellationToken cancellationToken)
    {
        var dhcpLeases = responseCache.GetDhcpLeases();
        if (dhcpLeases is null)
        {
            dhcpLeases = await FetchDhcpLeases(config, cancellationToken);
            responseCache.SetDhcpLeases(dhcpLeases);
        }

        var originalPhysicalAddresses = responseCache.GetOriginalPhysicalAddresses();
        if (originalPhysicalAddresses is null)
        {
            originalPhysicalAddresses = await FetchOriginalPhysicalAddresses(config, cancellationToken);
            responseCache.SetOriginalPhysicalAddresses(originalPhysicalAddresses);
        }

        var ntpFallback = responseCache.GetNtpFallbackServers();
        if (ntpFallback is null)
        {
            ntpFallback = await FetchNtpFallbackServers(cancellationToken);
            responseCache.SetNtpFallbackServers(ntpFallback);
        }

        return new SystemConfigurationAdditionalData(dhcpLeases, originalPhysicalAddresses, ntpFallback);
    }

    private async Task<Dictionary<string, DHCPLease?>> FetchDhcpLeases(SystemConfiguration? config, CancellationToken cancellationToken)
    {
        var leases = new Dictionary<string, DHCPLease?>();

        if (config is null)
            return leases;

        foreach (var nInterface in config.NetworkInterfaces)
        {
            if (!nInterface.IPv4.DHCPEnabled)
                continue;

            var result = await pipeClient.GetDHCPLeaseInformation(nInterface.CommonInformation.Name, cancellationToken);
            leases[nInterface.CommonInformation.Name] = result is { Status: OperationStatus.Success }
                ? result.DHCPLease
                : null;
        }

        return leases;
    }

    private async Task<Dictionary<string, PhysicalAddress?>> FetchOriginalPhysicalAddresses(SystemConfiguration? config, CancellationToken cancellationToken)
    {
        var originalPhysicalAddresses = new Dictionary<string, PhysicalAddress?>();

        if (config is null)
            return originalPhysicalAddresses;

        foreach (var nInterface in config.NetworkInterfaces)
        {
            if (nInterface.CommonInformation.UserDefinedMACAddress.Enabled)
                continue;

            var result = await pipeClient.GetOriginalPhysicalAddress(nInterface.CommonInformation.Name, cancellationToken);
            originalPhysicalAddresses[nInterface.CommonInformation.Name] = result is { Status: OperationStatus.Success }
                ? result.OriginalPhysicalAddress
                : null;
        }

        return originalPhysicalAddresses;
    }

    private async Task<List<string>> FetchNtpFallbackServers(CancellationToken cancellationToken)
    {
        var result = await pipeClient.GetNTPFallbackInformation(cancellationToken);
        return result is { Status: OperationStatus.Success }
            ? result.FallbackNTPServers
            : [];
    }

    public override Task<GetSystemConfigurationResponse> HandleException(GetSystemConfiguration message,
        Exception e, CancellationToken cancellationToken)
    {
        LogExceptionOccurred(logger, e);

        return Task.FromResult(new GetSystemConfigurationResponse { RequestError = new ErrorInfo(4, e.Message) });
    }

    [LoggerMessage(LogLevel.Error, "Deserialize() returned null")]
    private static partial void LogDeserializeReturnedNull(ILogger<GetSystemConfigurationRequestConsumer> logger);

    [LoggerMessage(LogLevel.Warning, "System configuration with warnings returned ({Message})")]
    private static partial void LogWarningStatusReturned(ILogger<GetSystemConfigurationRequestConsumer> logger, string? message);

    [LoggerMessage(LogLevel.Error, "Error status returned ({Message})")]
    private static partial void LogErrorStatusReturned(ILogger<GetSystemConfigurationRequestConsumer> logger, string? message);

    [LoggerMessage(LogLevel.Error, "An exception was thrown while fetching system configuration from Host Management")]
    private static partial void LogExceptionOccurred(ILogger<GetSystemConfigurationRequestConsumer> logger, Exception exception);
}
