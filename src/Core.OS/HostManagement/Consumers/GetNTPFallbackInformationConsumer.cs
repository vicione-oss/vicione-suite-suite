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
            LogDeserializeReturnedNull(logger);

            return new GetNTPFallbackInformationResponse { RequestError = new ErrorInfo(1, "Deserialization failed") };
        }

        if (getNTPFallbackInformation.Status == OperationStatus.Success)
            return new GetNTPFallbackInformationResponse { FallbackNTPServers = getNTPFallbackInformation.FallbackNTPServers };

        if (getNTPFallbackInformation.Status == OperationStatus.Warning)
        {
            LogWarningStatusReturned(logger, getNTPFallbackInformation.Message);

            return new GetNTPFallbackInformationResponse { FallbackNTPServers = getNTPFallbackInformation.FallbackNTPServers };
        }

        // OperationStatus.Error
        LogErrorStatusReturned(logger, getNTPFallbackInformation.Message);

        return new GetNTPFallbackInformationResponse { RequestError = new ErrorInfo(3, getNTPFallbackInformation.Message) };
    }

    public override Task<GetNTPFallbackInformationResponse> HandleException(GetNTPFallbackInformation message, Exception e, CancellationToken cancellationToken)
    {
        LogExceptionOccurred(logger, e);

        return Task.FromResult(new GetNTPFallbackInformationResponse { RequestError = new ErrorInfo(4, e.Message) });
    }

    [LoggerMessage(LogLevel.Error, "Deserialize() returned null")]
    private static partial void LogDeserializeReturnedNull(ILogger<GetNTPFallbackInformationConsumer> logger);

    [LoggerMessage(LogLevel.Warning, "Network status information with warnings returned ({Message})")]
    private static partial void LogWarningStatusReturned(ILogger<GetNTPFallbackInformationConsumer> logger, string? message);

    [LoggerMessage(LogLevel.Error, "Error status returned ({Message})")]
    private static partial void LogErrorStatusReturned(ILogger<GetNTPFallbackInformationConsumer> logger, string? message);

    [LoggerMessage(LogLevel.Error, "An exception was thrown while renew DHCP lease from Host Management")]
    private static partial void LogExceptionOccurred(ILogger<GetNTPFallbackInformationConsumer> logger, Exception exception);
}
