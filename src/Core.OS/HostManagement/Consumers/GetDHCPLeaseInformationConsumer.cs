using Core.OS.HostManagement.Extensions;
using Core.Shared.HostManagement.Requests;
using HostManagement.Shared.Communication.Enums;
using MassTransit;
using Sdk.Messaging;

namespace Core.OS.HostManagement.Consumers;

public partial class GetDHCPLeaseInformationConsumer(IPipeClient pipeClient, ILogger<GetDHCPLeaseInformationConsumer> logger) : RequestConsumer<GetDHCPLeaseInformation, GetDHCPLeaseInformationResponse>
{
    protected override async Task<GetDHCPLeaseInformationResponse> Respond(ConsumeContext<GetDHCPLeaseInformation> context)
    {
        if (string.IsNullOrWhiteSpace(context.Message.NetworkInterfaceName))
        {
            return new GetDHCPLeaseInformationResponse { RequestError = new ErrorInfo(5, "Network interface name is null or empty") };
        }

        LogPipeRequest(logger, context.Message.NetworkInterfaceName);

        var getDhcpLeaseResult = await pipeClient.GetDHCPLeaseInformation(context.Message.NetworkInterfaceName);
        if (getDhcpLeaseResult is null)
        {
            LogDeserializeReturnedNull(logger);

            return new GetDHCPLeaseInformationResponse { RequestError = new ErrorInfo(1, "Deserialization failed") };
        }

        if (getDhcpLeaseResult.Status == OperationStatus.Success)
            return new GetDHCPLeaseInformationResponse { DHCPLease = getDhcpLeaseResult.DHCPLease };

        if (getDhcpLeaseResult.Status == OperationStatus.Warning)
        {
            LogWarningStatusReturned(logger, getDhcpLeaseResult.Message);

            return new GetDHCPLeaseInformationResponse { DHCPLease = getDhcpLeaseResult.DHCPLease };
        }

        // OperationStatus.Error
        LogErrorStatusReturned(logger, getDhcpLeaseResult.Message);

        return new GetDHCPLeaseInformationResponse { RequestError = new ErrorInfo(3, getDhcpLeaseResult.Message) };
    }

    protected override Task<GetDHCPLeaseInformationResponse> HandleException(ConsumeContext<GetDHCPLeaseInformation> context, Exception e)
    {
        LogExceptionOccurred(logger, e);

        if (e.InnerException is not null)
            LogExceptionOccurred(logger, e.InnerException);

        return Task.FromResult(new GetDHCPLeaseInformationResponse { RequestError = new ErrorInfo(4, e.Message) });
    }

    [LoggerMessage(LogLevel.Information, "Sending pipe request for network interface '{networkInterfaceName}'")]
    private static partial void LogPipeRequest(ILogger<GetDHCPLeaseInformationConsumer> logger, string networkInterfaceName);

    [LoggerMessage(LogLevel.Error, "Deserialize() returned null")]
    private static partial void LogDeserializeReturnedNull(ILogger<GetDHCPLeaseInformationConsumer> logger);

    [LoggerMessage(LogLevel.Warning, "Network status information with warnings returned ({message})")]
    private static partial void LogWarningStatusReturned(ILogger<GetDHCPLeaseInformationConsumer> logger, string? message);

    [LoggerMessage(LogLevel.Error, "Error status returned ({message})")]
    private static partial void LogErrorStatusReturned(ILogger<GetDHCPLeaseInformationConsumer> logger, string? message);

    [LoggerMessage(LogLevel.Error, "An exception was thrown while renew DHCP lease from Host Management")]
    private static partial void LogExceptionOccurred(ILogger<GetDHCPLeaseInformationConsumer> logger, Exception exception);

}

