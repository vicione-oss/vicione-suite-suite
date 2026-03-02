using Core.OS.HostManagement.Extensions;
using Core.OS.HostManagement.Mappers;
using HostManagement.Shared.Communication.Enums;
using MassTransit;
using Sdk.Backend.Messaging;
using Sdk.Messaging;
using Sdk.NetworkStatus.Requests;

namespace Core.OS.HostManagement.Consumers;

public sealed partial class GetNetworkStatusInformationRequestConsumer(IPipeClient pipeClient, ILogger<GetNetworkStatusInformationRequestConsumer> logger)
    : RequestConsumer<GetNetworkStatusInformation, GetNetworkStatusInformationResponse>
{
    protected override async Task<GetNetworkStatusInformationResponse> Respond(ConsumeContext<GetNetworkStatusInformation> context)
    {
        var mapper = new NetworkStatusInformationMapper();

        var result = await pipeClient.GetNetworkStatusInformation(context.Message.NetworkInterfaceName, context.CancellationToken);
        if (result == null)
        {
            DeserializeReturnedNull(logger);

            return new GetNetworkStatusInformationResponse { RequestError = new ErrorInfo(1, "Deserialization failed") };
        }

        if (result.Status == OperationStatus.Success)
            return new GetNetworkStatusInformationResponse { NetworkStatusInformation = mapper.ToSuiteFormat(result.NetworkStatusInformation) };

        if (result.Status == OperationStatus.Warning)
        {
            WarningStatusReturned(logger, result.Message);

            return new GetNetworkStatusInformationResponse { NetworkStatusInformation = mapper.ToSuiteFormat(result.NetworkStatusInformation) };
        }

        // OperationStatus.Error
        ErrorStatusReturned(logger, result.Message);

        return new GetNetworkStatusInformationResponse { RequestError = new ErrorInfo(3, result.Message) };
    }

    protected override Task<GetNetworkStatusInformationResponse> HandleException(ConsumeContext<GetNetworkStatusInformation> context,
        Exception e)
    {
        ExceptionOccurred(logger, e);

        if (e.InnerException is not null)
            ExceptionOccurred(logger, e.InnerException);

        return Task.FromResult(new GetNetworkStatusInformationResponse { RequestError = new ErrorInfo(4, e.Message) });
    }

    [LoggerMessage(1, LogLevel.Error, "Deserialize() returned null")]
    private static partial void DeserializeReturnedNull(ILogger<GetNetworkStatusInformationRequestConsumer> logger);

    [LoggerMessage(2, LogLevel.Warning, "Network status information with warnings returned ({message})")]
    private static partial void WarningStatusReturned(ILogger<GetNetworkStatusInformationRequestConsumer> logger, string? message);

    [LoggerMessage(3, LogLevel.Error, "Error status returned ({message})")]
    private static partial void ErrorStatusReturned(ILogger<GetNetworkStatusInformationRequestConsumer> logger, string? message);

    [LoggerMessage(4, LogLevel.Error, "An exception was thrown while fetching network status information from Host Management")]
    private static partial void ExceptionOccurred(ILogger<GetNetworkStatusInformationRequestConsumer> logger, Exception exception);
}
