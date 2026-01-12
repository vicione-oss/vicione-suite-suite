using Core.OS.HostManagement.Extensions;
using Core.Shared.HostManagement.Requests;
using HostManagement.Shared.Communication.Enums;
using MassTransit;
using Sdk.Messaging;

namespace Core.OS.HostManagement.Consumers;

public partial class GetOriginalPhysicalAddressConsumer(IPipeClient pipeClient, ILogger<GetOriginalPhysicalAddressConsumer> logger) : RequestConsumer<GetOriginalPhysicalAddress, GetOriginalPhysicalAddressResponse>
{
    protected override async Task<GetOriginalPhysicalAddressResponse> Respond(ConsumeContext<GetOriginalPhysicalAddress> context)
    {
        if (string.IsNullOrWhiteSpace(context.Message.NetworkInterfaceName))
        {
            return new GetOriginalPhysicalAddressResponse { RequestError = new ErrorInfo(5, "Network interface name is null or empty") };
        }

        LogPipeRequest(logger, context.Message.NetworkInterfaceName);

        var getOriginalPhysicalAddressResult = await pipeClient.GetOriginalPhysicalAddress(context.Message.NetworkInterfaceName, context.CancellationToken);
        if (getOriginalPhysicalAddressResult is null)
        {
            LogDeserializeReturnedNull(logger);

            return new GetOriginalPhysicalAddressResponse { RequestError = new ErrorInfo(1, "Deserialization failed") };
        }

        if (getOriginalPhysicalAddressResult.Status == OperationStatus.Success)
            return new GetOriginalPhysicalAddressResponse { OriginalPhysicalAddress = getOriginalPhysicalAddressResult.OriginalPhysicalAddress };

        if (getOriginalPhysicalAddressResult.Status == OperationStatus.Warning)
        {
            LogWarningStatusReturned(logger, getOriginalPhysicalAddressResult.Message);

            return new GetOriginalPhysicalAddressResponse { OriginalPhysicalAddress = getOriginalPhysicalAddressResult.OriginalPhysicalAddress };
        }

        // OperationStatus.Error
        LogErrorStatusReturned(logger, getOriginalPhysicalAddressResult.Message);

        return new GetOriginalPhysicalAddressResponse { RequestError = new ErrorInfo(3, getOriginalPhysicalAddressResult.Message) };
    }

    protected override Task<GetOriginalPhysicalAddressResponse> HandleException(ConsumeContext<GetOriginalPhysicalAddress> context, Exception e)
    {
        LogExceptionOccurred(logger, e);

        if (e.InnerException is not null)
            LogExceptionOccurred(logger, e.InnerException);

        return Task.FromResult(new GetOriginalPhysicalAddressResponse { RequestError = new ErrorInfo(4, e.Message) });
    }

    [LoggerMessage(LogLevel.Information, "Sending pipe request for network interface '{networkInterfaceName}'")]
    private static partial void LogPipeRequest(ILogger<GetOriginalPhysicalAddressConsumer> logger, string networkInterfaceName);

    [LoggerMessage(LogLevel.Error, "Deserialize() returned null")]
    private static partial void LogDeserializeReturnedNull(ILogger<GetOriginalPhysicalAddressConsumer> logger);

    [LoggerMessage(LogLevel.Warning, "Network status information with warnings returned ({message})")]
    private static partial void LogWarningStatusReturned(ILogger<GetOriginalPhysicalAddressConsumer> logger, string? message);

    [LoggerMessage(LogLevel.Error, "Error status returned ({message})")]
    private static partial void LogErrorStatusReturned(ILogger<GetOriginalPhysicalAddressConsumer> logger, string? message);

    [LoggerMessage(LogLevel.Error, "An exception was thrown while renew DHCP lease from Host Management")]
    private static partial void LogExceptionOccurred(ILogger<GetOriginalPhysicalAddressConsumer> logger, Exception exception);
}

