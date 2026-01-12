using System.Text.Json;
using Core.OS.HostManagement.Extensions;
using Core.Shared.HostManagement.Requests;
using HostManagement.Shared.Communication;
using HostManagement.Shared.Communication.Contracts;
using HostManagement.Shared.Communication.Enums;
using MassTransit;
using Sdk.Messaging;

namespace Core.OS.HostManagement.Consumers;

public partial class GetNTPFallbackInformationConsumer(IPipeClient pipeClient, ILogger<GetNTPFallbackInformationConsumer> logger) : RequestConsumer<GetNTPFallbackInformation, GetNTPFallbackInformationResponse>
{
    protected override async Task<GetNTPFallbackInformationResponse> Respond(ConsumeContext<GetNTPFallbackInformation> context)
    {
        var getNTPFallbackInformation = await pipeClient.GetNTPFallbackInformation(context.CancellationToken);
        if (getNTPFallbackInformation is null)
        {
            DeserializeReturnedNull(logger);

            return new GetNTPFallbackInformationResponse { RequestError = new ErrorInfo(1, "Deserialization failed") };
        }

        if (getNTPFallbackInformation.Status == OperationStatus.Success)
            return new GetNTPFallbackInformationResponse { FallbackNTPServers = getNTPFallbackInformation.FallbackNTPServers };

        if (getNTPFallbackInformation.Status == OperationStatus.Warning)
        {
            WarningStatusReturned(logger, getNTPFallbackInformation.Message);

            return new GetNTPFallbackInformationResponse { FallbackNTPServers = getNTPFallbackInformation.FallbackNTPServers };
        }

        // OperationStatus.Error
        ErrorStatusReturned(logger, getNTPFallbackInformation.Message);

        return new GetNTPFallbackInformationResponse { RequestError = new ErrorInfo(3, getNTPFallbackInformation.Message) };
    }

    protected override Task<GetNTPFallbackInformationResponse> HandleException(ConsumeContext<GetNTPFallbackInformation> context, Exception e)
    {
        ExceptionOccurred(logger, e);

        return Task.FromResult(new GetNTPFallbackInformationResponse { RequestError = new ErrorInfo(4, e.Message) });
    }

    [LoggerMessage(1, LogLevel.Error, "Deserialize() returned null")]
    private static partial void DeserializeReturnedNull(ILogger<GetNTPFallbackInformationConsumer> logger);

    [LoggerMessage(2, LogLevel.Warning, "Network status information with warnings returned ({message})")]
    private static partial void WarningStatusReturned(ILogger<GetNTPFallbackInformationConsumer> logger, string? message);

    [LoggerMessage(3, LogLevel.Error, "Error status returned ({message})")]
    private static partial void ErrorStatusReturned(ILogger<GetNTPFallbackInformationConsumer> logger, string? message);

    [LoggerMessage(4, LogLevel.Error, "An exception was thrown while renew DHCP lease from Host Management")]
    private static partial void ExceptionOccurred(ILogger<GetNTPFallbackInformationConsumer> logger, Exception exception);
}
