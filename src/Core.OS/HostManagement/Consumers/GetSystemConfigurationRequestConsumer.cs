using Core.OS.HostManagement.Extensions;
using Core.OS.HostManagement.Mappers;
using HostManagement.Shared.Communication.Enums;
using MassTransit;
using Sdk.Messaging;
using Sdk.SystemConfiguration.Requests;

namespace Core.OS.HostManagement.Consumers;

public sealed partial class GetSystemConfigurationRequestConsumer(IPipeClient pipeClient, SystemConfigurationCache responseCache, ILogger<GetSystemConfigurationRequestConsumer> logger)
    : RequestConsumer<GetSystemConfiguration, GetSystemConfigurationResponse>
{
    protected override async Task<GetSystemConfigurationResponse> Respond(ConsumeContext<GetSystemConfiguration> context)
    {
        var mapper = new SystemConfigurationMapper();
        var config = responseCache.Get();
        if (config is not null)
        {
            return new GetSystemConfigurationResponse { Configuration = mapper.ToSuiteFormat(config) };
        }

        var configurationResult = await pipeClient.GetSystemConfiguration(context.CancellationToken);
        if (configurationResult == null)
        {
            LogDeserializeReturnedNull(logger);

            return new GetSystemConfigurationResponse { RequestError = new ErrorInfo(1, "Deserialization failed") };
        }

        if (configurationResult.Configuration is not null)
            responseCache.Set(configurationResult.Configuration);

        if (configurationResult.Status == OperationStatus.Success)
            return new GetSystemConfigurationResponse { Configuration = mapper.ToSuiteFormat(configurationResult.Configuration) };

        if (configurationResult.Status == OperationStatus.Warning)
        {
            LogWarningStatusReturned(logger, configurationResult.Message);

            return new GetSystemConfigurationResponse { Configuration = mapper.ToSuiteFormat(configurationResult.Configuration) };
        }

        // OperationStatus.Error
        LogErrorStatusReturned(logger, configurationResult.Message);

        return new GetSystemConfigurationResponse { RequestError = new ErrorInfo(3, configurationResult.Message) };
    }

    protected override Task<GetSystemConfigurationResponse> HandleException(ConsumeContext<GetSystemConfiguration> context,
        Exception e)
    {
        LogExceptionOccurred(logger, e);

        return Task.FromResult(new GetSystemConfigurationResponse { RequestError = new ErrorInfo(4, e.Message) });
    }

    [LoggerMessage(1, LogLevel.Error, "Deserialize() returned null")]
    private static partial void LogDeserializeReturnedNull(ILogger<GetSystemConfigurationRequestConsumer> logger);

    [LoggerMessage(2, LogLevel.Warning, "System configuration with warnings returned ({message})")]
    private static partial void LogWarningStatusReturned(ILogger<GetSystemConfigurationRequestConsumer> logger, string? message);

    [LoggerMessage(3, LogLevel.Error, "Error status returned ({message})")]
    private static partial void LogErrorStatusReturned(ILogger<GetSystemConfigurationRequestConsumer> logger, string? message);

    [LoggerMessage(4, LogLevel.Error, "An exception was thrown while fetching system configuration from Host Management")]
    private static partial void LogExceptionOccurred(ILogger<GetSystemConfigurationRequestConsumer> logger, Exception exception);

}
