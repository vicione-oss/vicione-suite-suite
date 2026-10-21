using System.Text.Json;
using Core.OS.Instance;
using HostManagement.Shared.Capabilities;
using HostManagement.Shared.Communication.Capabilities;
using HostManagement.Shared.Communication.Contracts;
using HostManagement.Shared.Communication.Enums;
using HostManagement.Shared.Communication.NamedPipe.Client;
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
        public Task<SystemControlResult> InstallSignedDebianPackage(SignedDebianPackage debianPackage, CancellationToken cancellationToken = default)
            => pipeClient.SendRequest(
                PipeRequests.InstallSignedDebianPackage,
                JsonSerializer.Serialize(debianPackage, SharedJsonContext.Default.SignedDebianPackage),
                cancellationToken);

        public Task<SetSystemConfigurationResult> SetSystemConfiguration(SystemConfiguration systemConfiguration, CancellationToken cancellationToken = default)
            => pipeClient.SendRequest(
                PipeRequests.SetSystemConfiguration,
                JsonSerializer.Serialize(systemConfiguration, CommunicationJsonContext.Default.SystemConfiguration),
                cancellationToken);

        public Task<GetSystemConfigurationResult> GetSystemConfiguration(CancellationToken cancellationToken = default)
            => pipeClient.SendRequest(PipeRequests.GetSystemConfiguration, cancellationToken: cancellationToken);

        public Task<GetDHCPLeaseInformationResult> GetDHCPLeaseInformation(string networkInterfaceName, CancellationToken cancellationToken = default)
            => pipeClient.SendRequest(PipeRequests.GetDHCPLeaseInformation, networkInterfaceName, cancellationToken);

        public Task<GetNetworkStatusInformationResult> GetNetworkStatusInformation(string networkInterfaceName, CancellationToken cancellationToken = default)
            => pipeClient.SendRequest(PipeRequests.GetNetworkStatusInformation, networkInterfaceName, cancellationToken);

        public Task<GetNTPFallbackInformationResult> GetNTPFallbackInformation(CancellationToken cancellationToken = default)
            => pipeClient.SendRequest(PipeRequests.GetNTPFallbackInformation, cancellationToken: cancellationToken);

        public Task<GetSupportedCapabilitiesResult> GetSupportedCapabilities(CancellationToken cancellationToken = default)
            => pipeClient.SendRequest(CapabilityPipeRequests.GetSupportedCapabilities, cancellationToken: cancellationToken);

        /// <returns>
        /// The capabilities, or <see langword="null"/> when they cannot be read.
        /// HostManagement still rejects disabled requests, so callers skip their own check on <see langword="null"/>.
        /// </returns>
        public async Task<SupportedCapabilities?> GetSupportedCapabilitiesOrNull(ILogger logger, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await pipeClient.GetSupportedCapabilities(cancellationToken);
                if (result.Status == OperationStatus.Success)
                    return result.SupportedCapabilities;

                LogSupportedCapabilitiesNotReturned(logger, result.Status, result.Message);
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

        public Task<GetOriginalPhysicalAddressResult> GetOriginalPhysicalAddress(string networkInterfaceName, CancellationToken cancellationToken = default)
            => pipeClient.SendRequest(PipeRequests.GetOriginalPhysicalAddress, networkInterfaceName, cancellationToken);

        public Task<GetDHCPLeaseInformationResult> RenewDHCPLease(string networkInterfaceName, CancellationToken cancellationToken = default)
            => pipeClient.SendRequest(PipeRequests.RenewDHCPLease, networkInterfaceName, cancellationToken);

        public Task<SystemControlResult> UpdateSystem(string filePath, CancellationToken cancellationToken = default)
            => pipeClient.SendRequest(PipeRequests.UpdateSystem, filePath, cancellationToken);

        public Task<SystemControlResult> ResetSystem(CancellationToken cancellationToken = default)
            => pipeClient.SendRequest(PipeRequests.ResetSystem, cancellationToken: cancellationToken);

        public Task<SystemControlResult> RestartSystem(CancellationToken cancellationToken = default)
            => pipeClient.SendRequest(PipeRequests.RestartSystem, SystemControlDelaySeconds, cancellationToken);

        public Task<ServiceControlResult> RestartService(string serviceName, CancellationToken cancellationToken = default)
            => pipeClient.SendRequest(PipeRequests.RestartService, serviceName, cancellationToken);

        public Task<ServiceControlResult> RestartSuite(InstanceOptions options, CancellationToken cancellationToken = default)
            => pipeClient.RestartService(options.ServiceName, cancellationToken);

        public Task<SystemControlResult> ShutdownSystem(CancellationToken cancellationToken = default)
            => pipeClient.SendRequest(PipeRequests.ShutdownSystem, SystemControlDelaySeconds, cancellationToken);

        public Task<ServiceControlResult> StartService(string serviceName, CancellationToken cancellationToken = default)
            => pipeClient.SendRequest(PipeRequests.StartService, serviceName, cancellationToken);

        public Task<ServiceControlResult> StopService(string serviceName, CancellationToken cancellationToken = default)
            => pipeClient.SendRequest(PipeRequests.StopService, serviceName, cancellationToken);
    }

    [LoggerMessage(LogLevel.Warning, "HostManagement returned no supported capabilities ({Status}: {Message}), skipping the capability check")]
    private static partial void LogSupportedCapabilitiesNotReturned(ILogger logger, OperationStatus? status, string? message);

    [LoggerMessage(LogLevel.Warning, "Reading the supported capabilities from HostManagement failed, skipping the capability check")]
    private static partial void LogSupportedCapabilitiesRequestFailed(ILogger logger, Exception exception);
}
