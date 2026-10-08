using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Core.OS.Instance;
using HostManagement.Shared.Capabilities;
using HostManagement.Shared.Communication;
using HostManagement.Shared.Communication.Capabilities;
using HostManagement.Shared.Communication.Contracts;
using HostManagement.Shared.Communication.Enums;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.System;
using CommunicationJsonContext = HostManagement.Shared.Communication.Contracts.SourceGenerationContext;
using SharedJsonContext = HostManagement.Shared.Contracts.SourceGenerationContext;

namespace Core.OS.HostManagement.Extensions;

internal static partial class IPipeClientExtensions
{
    /// <summary>
    /// Seconds HostManagement waits before a device restart or shutdown, so its response reaches the Suite before the device goes down.
    /// </summary>
    internal const string SystemControlDelaySeconds = "3";

    extension(IPipeClient pipeClient)
    {
        public async Task<SystemControlResult?> InstallSignedDebianPackage(SignedDebianPackage debianPackage, CancellationToken cancellationToken = default)
        {
            var requestJson = JsonSerializer.Serialize(debianPackage, SharedJsonContext.Default.SignedDebianPackage);

            return await GetRequestResult(
                () => pipeClient.SendRequest(Topics.InstallSignedDebianPackage, requestJson, cancellationToken),
                CommunicationJsonContext.Default.SystemControlResult);
        }

        public async Task<SetSystemConfigurationResult?> SetSystemConfiguration(SystemConfiguration systemConfiguration, CancellationToken cancellationToken = default)
        {
            var configJson = JsonSerializer.Serialize(systemConfiguration, CommunicationJsonContext.Default.SystemConfiguration);

            return await GetRequestResult(
                () => pipeClient.SendRequest(Topics.SetSystemConfiguration, configJson, cancellationToken),
                CommunicationJsonContext.Default.SetSystemConfigurationResult);
        }

        public async Task<GetSystemConfigurationResult?> GetSystemConfiguration(CancellationToken cancellationToken = default)
            => await GetRequestResult(
                () => pipeClient.SendRequest(Topics.GetSystemConfiguration, string.Empty, cancellationToken),
                CommunicationJsonContext.Default.GetSystemConfigurationResult);

        public async Task<GetDHCPLeaseInformationResult?> GetDHCPLeaseInformation(string networkInterfaceName, CancellationToken cancellationToken = default)
            => await GetRequestResult(
                () => pipeClient.SendRequest(Topics.GetDHCPLeaseInformation, networkInterfaceName, cancellationToken),
                CommunicationJsonContext.Default.GetDHCPLeaseInformationResult);

        public async Task<GetNetworkStatusInformationResult?> GetNetworkStatusInformation(string networkInterfaceName, CancellationToken cancellationToken = default)
            => await GetRequestResult(
                () => pipeClient.SendRequest(Topics.GetNetworkStatusInformation, networkInterfaceName, cancellationToken),
                CommunicationJsonContext.Default.GetNetworkStatusInformationResult);

        public async Task<GetNTPFallbackInformationResult?> GetNTPFallbackInformation(CancellationToken cancellationToken = default)
            => await GetRequestResult(
                () => pipeClient.SendRequest(Topics.GetNTPFallbackInformation, string.Empty, cancellationToken),
                CommunicationJsonContext.Default.GetNTPFallbackInformationResult);

        public async Task<GetSupportedCapabilitiesResult?> GetSupportedCapabilities(CancellationToken cancellationToken = default)
            => await GetRequestResult(
                () => pipeClient.SendRequest(Topics.GetSupportedCapabilities, string.Empty, cancellationToken),
                CapabilitySourceGenerationContext.Default.GetSupportedCapabilitiesResult);

        /// <returns>
        /// The capabilities, or <see langword="null"/> when they cannot be read.
        /// HostManagement still rejects disabled requests, so callers skip their own check on <see langword="null"/>.
        /// </returns>
        public async Task<SupportedCapabilities?> GetSupportedCapabilitiesOrNull(ILogger logger, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await pipeClient.GetSupportedCapabilities(cancellationToken);
                if (result?.Status == OperationStatus.Success)
                    return result.SupportedCapabilities;

                LogSupportedCapabilitiesNotReturned(logger, result?.Status, result?.Message);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogSupportedCapabilitiesRequestFailed(logger, exception);
            }

            return null;
        }

        /// <returns>
        /// <see langword="false"/> when the capabilities cannot be read, see <see cref="GetSupportedCapabilitiesOrNull"/>.
        /// </returns>
        public async Task<bool> IsDisabled(Func<SupportedTopics, CapabilityStatus> topic, ILogger logger, CancellationToken cancellationToken = default)
        {
            var capabilities = await pipeClient.GetSupportedCapabilitiesOrNull(logger, cancellationToken);

            return capabilities is not null && topic(capabilities.Topics) is CapabilityStatus.Disabled;
        }

        public async Task<GetOriginalPhysicalAddressResult?> GetOriginalPhysicalAddress(string networkInterfaceName, CancellationToken cancellationToken = default)
            => await GetRequestResult(
                () => pipeClient.SendRequest(Topics.GetOriginalPhysicalAddress, networkInterfaceName, cancellationToken),
                CommunicationJsonContext.Default.GetOriginalPhysicalAddressResult);

        public async Task<GetDHCPLeaseInformationResult?> RenewDHCPLease(string networkInterfaceName, CancellationToken cancellationToken = default)
            => await GetRequestResult(
                () => pipeClient.SendRequest(Topics.RenewDHCPLease, networkInterfaceName, cancellationToken),
                CommunicationJsonContext.Default.GetDHCPLeaseInformationResult);

        public async Task<SystemControlResult?> UpdateSystem(string filePath, CancellationToken cancellationToken = default)
            => await GetRequestResult(
                () => pipeClient.SendRequest(Topics.UpdateSystem, filePath, cancellationToken),
                CommunicationJsonContext.Default.SystemControlResult);

        public async Task<SystemControlResult?> ResetSystem(CancellationToken cancellationToken = default)
            => await GetRequestResult(
                () => pipeClient.SendRequest(Topics.ResetSystem, string.Empty, cancellationToken),
                CommunicationJsonContext.Default.SystemControlResult);

        public async Task<SystemControlResult?> RestartSystem(CancellationToken cancellationToken = default)
            => await GetRequestResult(
                () => pipeClient.SendRequest(Topics.RestartSystem, SystemControlDelaySeconds, cancellationToken),
                CommunicationJsonContext.Default.SystemControlResult);

        public async Task<ServiceControlResult> RestartService(string serviceName, CancellationToken cancellationToken = default)
            => (await GetRequestResult(
                   () => pipeClient.SendRequest(Topics.RestartService, serviceName, cancellationToken),
                   CommunicationJsonContext.Default.ServiceControlResult))
               ?? throw new InvalidOperationException($"Send restart service='{serviceName}' request returned null");

        public Task<ServiceControlResult> RestartSuite(InstanceOptions options, CancellationToken cancellationToken = default)
            => pipeClient.RestartService(options.ServiceName, cancellationToken);

        public async Task<SystemControlResult?> ShutdownSystem(CancellationToken cancellationToken = default)
            => await GetRequestResult(
                () => pipeClient.SendRequest(Topics.ShutdownSystem, SystemControlDelaySeconds, cancellationToken),
                CommunicationJsonContext.Default.SystemControlResult);

        public async Task<ServiceControlResult?> StartService(string serviceName, CancellationToken cancellationToken = default)
            => await GetRequestResult(
                () => pipeClient.SendRequest(Topics.StartService, serviceName, cancellationToken),
                CommunicationJsonContext.Default.ServiceControlResult);

        public async Task<ServiceControlResult?> StopService(string serviceName, CancellationToken cancellationToken = default)
            => await GetRequestResult(
                () => pipeClient.SendRequest(Topics.StopService, serviceName, cancellationToken),
                CommunicationJsonContext.Default.ServiceControlResult);
    }

    private static async Task<TResult?> GetRequestResult<TResult>(Func<Task<string>> pipeRequest, JsonTypeInfo<TResult> typeInfo)
        where TResult : class
    {
        var settingsJson = await pipeRequest();

        try
        {
            return JsonSerializer.Deserialize(settingsJson, typeInfo);
        }
        catch (JsonException jsonEx)
        {
            // HostManagement answers a rejected request, such as one for a disabled topic, with a plain Response carrying the reason.
            var error = JsonSerializer.Deserialize(settingsJson, CommunicationJsonContext.Default.Response);
            throw new InvalidOperationException($"Pipe request failed with error: {error?.Message}", jsonEx);
        }
    }

    [LoggerMessage(LogLevel.Warning, "HostManagement returned no supported capabilities ({Status}: {Message}), skipping the capability check")]
    private static partial void LogSupportedCapabilitiesNotReturned(ILogger logger, OperationStatus? status, string? message);

    [LoggerMessage(LogLevel.Warning, "Reading the supported capabilities from HostManagement failed, skipping the capability check")]
    private static partial void LogSupportedCapabilitiesRequestFailed(ILogger logger, Exception exception);
}
