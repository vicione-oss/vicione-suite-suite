using Core.OS.HostManagement.Extensions;
using Core.Shared.HostManagement.Requests;
using HostManagement.Shared.Communication.Enums;
using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace Core.OS.HostManagement.Consumers;

public sealed partial class RenewDHCPLeaseConsumer(IPipeClient pipeClient, ILogger<RenewDHCPLeaseConsumer> logger)
    : RequestConsumer<RenewDHCPLease, RenewDHCPLeaseResponse>
{
    public override async Task<RenewDHCPLeaseResponse> Respond(RenewDHCPLease message, CancellationToken cancellationToken)
    {
        var dhcpLeaseResult = await pipeClient.RenewDHCPLease(message.NetworkInterfaceName, cancellationToken);
        if (dhcpLeaseResult.Status == OperationStatus.Success)
            return new RenewDHCPLeaseResponse { DHCPLease = dhcpLeaseResult.DHCPLease };

        if (dhcpLeaseResult.Status == OperationStatus.Warning)
        {
            LogWarningStatusReturned(logger, dhcpLeaseResult.Message);

            return new RenewDHCPLeaseResponse { DHCPLease = dhcpLeaseResult.DHCPLease };
        }

        // OperationStatus.Error
        LogErrorStatusReturned(logger, dhcpLeaseResult.Message);

        return new RenewDHCPLeaseResponse { RequestError = new ErrorInfo(3, dhcpLeaseResult.Message) };
    }

    public override Task<RenewDHCPLeaseResponse> HandleException(RenewDHCPLease message, Exception e, CancellationToken cancellationToken)
    {
        LogExceptionOccurred(logger, e);

        if (e.InnerException is not null)
            LogExceptionOccurred(logger, e.InnerException);

        return Task.FromResult(new RenewDHCPLeaseResponse { RequestError = new ErrorInfo(4, e.Message) });
    }

    [LoggerMessage(LogLevel.Warning, "Network status information with warnings returned ({Message})")]
    private static partial void LogWarningStatusReturned(ILogger<RenewDHCPLeaseConsumer> logger, string? message);

    [LoggerMessage(LogLevel.Error, "Error status returned ({Message})")]
    private static partial void LogErrorStatusReturned(ILogger<RenewDHCPLeaseConsumer> logger, string? message);

    [LoggerMessage(LogLevel.Error, "An exception was thrown while renew DHCP lease from Host Management")]
    private static partial void LogExceptionOccurred(ILogger<RenewDHCPLeaseConsumer> logger, Exception exception);
}

