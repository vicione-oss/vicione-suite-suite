using Core.OS.HostManagement.Extensions;
using Core.Shared.HostManagement.Requests;
using HostManagement.Shared.Communication.Enums;
using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace Core.OS.HostManagement.Consumers;

public partial class GetNTPFallbackInformationConsumer(IPipeClient pipeClient, ILogger<GetNTPFallbackInformationConsumer> logger) : RequestConsumer<GetNTPFallbackInformation, GetNTPFallbackInformationResponse>
{
    public override async Task<GetNTPFallbackInformationResponse> Respond(GetNTPFallbackInformation message, CancellationToken cancellationToken)
    {
        var getNTPFallbackInformation = await pipeClient.GetNTPFallbackInformation(cancellationToken);
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

    public override Task<GetNTPFallbackInformationResponse> HandleException(GetNTPFallbackInformation message, Exception e, CancellationToken cancellationToken)
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
