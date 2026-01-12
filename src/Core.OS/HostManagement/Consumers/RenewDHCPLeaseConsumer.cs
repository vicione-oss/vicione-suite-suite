using System.Text.Json;
using Core.Shared.HostManagement.Requests;
using Core.Shared.HostManagement.Services;
using HostManagement.Shared.Communication;
using HostManagement.Shared.Communication.Contracts;
using HostManagement.Shared.Communication.Enums;
using MassTransit;
using Sdk.Messaging;

namespace Core.OS.HostManagement.Consumers;

public sealed partial class RenewDHCPLeaseConsumer(IPipeClient pipeClient, ISystemConfigurationService systemConfigurationService, ILogger<RenewDHCPLeaseConsumer> logger)
    : RequestConsumer<RenewDHCPLease, RenewDHCPLeaseResponse>
{
    [LoggerMessage(1, LogLevel.Error, "Deserialize() returned null")]
    private static partial void DeserializeReturnedNull(ILogger<RenewDHCPLeaseConsumer> logger);

    [LoggerMessage(2, LogLevel.Warning, "Network status information with warnings returned ({message})")]
    private static partial void WarningStatusReturned(ILogger<RenewDHCPLeaseConsumer> logger, string? message);

    [LoggerMessage(3, LogLevel.Error, "Error status returned ({message})")]
    private static partial void ErrorStatusReturned(ILogger<RenewDHCPLeaseConsumer> logger, string? message);

    [LoggerMessage(4, LogLevel.Error, "An exception was thrown while renew DHCP lease from Host Management")]
    private static partial void ExceptionOccurred(ILogger<RenewDHCPLeaseConsumer> logger, Exception exception);

    protected override async Task<RenewDHCPLeaseResponse> Respond(ConsumeContext<RenewDHCPLease> context)
    {
        var settingsJson = await pipeClient.SendRequest(Topics.RenewDHCPLease, context.Message.NetworkInterfaceName, context.CancellationToken);
        var dhcpLeaseResult = JsonSerializer.Deserialize(settingsJson, SourceGenerationContext.Default.RenewDHCPLeaseResult);

        if (dhcpLeaseResult is null)
        {
            DeserializeReturnedNull(logger);

            return new RenewDHCPLeaseResponse { RequestError = new ErrorInfo(1, "Deserialization failed") };
        }

        if (dhcpLeaseResult.Status == OperationStatus.Success)
        {
            if (dhcpLeaseResult.DHCPLease is not null)
                await systemConfigurationService.SetDhcpLease(dhcpLeaseResult.DHCPLease, context.Message.NetworkInterfaceName);

            return new RenewDHCPLeaseResponse { DHCPLease = dhcpLeaseResult.DHCPLease };
        }

        if (dhcpLeaseResult.Status == OperationStatus.Warning)
        {
            WarningStatusReturned(logger, dhcpLeaseResult.Message);

            return new RenewDHCPLeaseResponse { DHCPLease = dhcpLeaseResult.DHCPLease };
        }

        // OperationStatus.Error
        ErrorStatusReturned(logger, dhcpLeaseResult.Message);

        return new RenewDHCPLeaseResponse { RequestError = new ErrorInfo(3, dhcpLeaseResult.Message) };
    }

    protected override Task<RenewDHCPLeaseResponse> HandleException(ConsumeContext<RenewDHCPLease> context, Exception e)
    {
        ExceptionOccurred(logger, e);

        return Task.FromResult(new RenewDHCPLeaseResponse { RequestError = new ErrorInfo(4, e.Message) });
    }
}

