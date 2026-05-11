using Core.OS.HostManagement.Extensions;
using Core.OS.HostManagement.Mappers;
using HostManagement.Shared.Communication.Enums;
using Sdk.Backend.Messaging;
using Sdk.Messaging;
using Sdk.NetworkStatus.Requests;

namespace Core.OS.HostManagement.Consumers;

public sealed partial class GetNetworkStatusInformationRequestConsumer(IPipeClient pipeClient, ILogger<GetNetworkStatusInformationRequestConsumer> logger)
    : RequestConsumer<GetNetworkStatusInformation, GetNetworkStatusInformationResponse>
{
    public override async Task<GetNetworkStatusInformationResponse> Respond(GetNetworkStatusInformation message, CancellationToken cancellationToken)
    {
        var mapper = new NetworkStatusInformationMapper();

        var result = await pipeClient.GetNetworkStatusInformation(message.NetworkInterfaceName, cancellationToken);
        if (result == null)
        {
            LogDeserializeReturnedNull(logger);

            return new GetNetworkStatusInformationResponse { RequestError = new ErrorInfo(1, "Deserialization failed") };
        }

        if (result.Status == OperationStatus.Success)
            return new GetNetworkStatusInformationResponse { NetworkStatusInformation = mapper.ToSuiteFormat(result.NetworkStatusInformation) };

        if (result.Status == OperationStatus.Warning)
        {
            LogWarningStatusReturned(logger, result.Message);

            return new GetNetworkStatusInformationResponse { NetworkStatusInformation = mapper.ToSuiteFormat(result.NetworkStatusInformation) };
        }

        // OperationStatus.Error
        LogErrorStatusReturned(logger, result.Message);

        return new GetNetworkStatusInformationResponse { RequestError = new ErrorInfo(3, result.Message) };
    }

    public override Task<GetNetworkStatusInformationResponse> HandleException(GetNetworkStatusInformation message,
        Exception e, CancellationToken cancellationToken)
    {
        LogExceptionOccurred(logger, e);

        if (e.InnerException is not null)
            LogExceptionOccurred(logger, e.InnerException);

        return Task.FromResult(new GetNetworkStatusInformationResponse { RequestError = new ErrorInfo(4, e.Message) });
    }

    [LoggerMessage(LogLevel.Error, "Deserialize() returned null")]
    private static partial void LogDeserializeReturnedNull(ILogger<GetNetworkStatusInformationRequestConsumer> logger);

    [LoggerMessage(LogLevel.Warning, "Network status information with warnings returned ({Message})")]
    private static partial void LogWarningStatusReturned(ILogger<GetNetworkStatusInformationRequestConsumer> logger, string? message);

    [LoggerMessage(LogLevel.Error, "Error status returned ({Message})")]
    private static partial void LogErrorStatusReturned(ILogger<GetNetworkStatusInformationRequestConsumer> logger, string? message);

    [LoggerMessage(LogLevel.Error, "An exception was thrown while fetching network status information from Host Management")]
    private static partial void LogExceptionOccurred(ILogger<GetNetworkStatusInformationRequestConsumer> logger, Exception exception);
}
