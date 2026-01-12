using System.Text.Json;
using Core.OS.HostManagement.Mappers;
using HostManagement.Shared.Communication;
using HostManagement.Shared.Communication.Enums;
using MassTransit;
using Sdk.Messaging;
using Sdk.SystemConfiguration.Requests;
using SourceGenerationContext = HostManagement.Shared.Communication.Contracts.SourceGenerationContext;

namespace Core.OS.HostManagement.Consumers;

public sealed partial class GetSystemConfigurationRequestConsumer(IPipeClient pipeClient, SystemConfigurationCache responseCache, ILogger<GetSystemConfigurationRequestConsumer> logger)
    : RequestConsumer<GetSystemConfiguration, GetSystemConfigurationResponse>
{
    [LoggerMessage(1, LogLevel.Error, "Deserialize() returned null")]
    private static partial void DeserializeReturnedNull(ILogger<GetSystemConfigurationRequestConsumer> logger);

    [LoggerMessage(2, LogLevel.Warning, "System configuration with warnings returned ({message})")]
    private static partial void WarningStatusReturned(ILogger<GetSystemConfigurationRequestConsumer> logger, string? message);

    [LoggerMessage(3, LogLevel.Error, "Error status returned ({message})")]
    private static partial void ErrorStatusReturned(ILogger<GetSystemConfigurationRequestConsumer> logger, string? message);

    [LoggerMessage(4, LogLevel.Error, "An exception was thrown while fetching system configuration from Host Management")]
    private static partial void ExceptionOccurred(ILogger<GetSystemConfigurationRequestConsumer> logger, Exception exception);

    protected override async Task<GetSystemConfigurationResponse> Respond(ConsumeContext<GetSystemConfiguration> context)
    {
        var mapper = new SystemConfigurationMapper();
        var config = responseCache.Get();
        if (config is not null)
        {
            return new GetSystemConfigurationResponse { Configuration = mapper.ToSuiteFormat(config) };
        }

        var settingsJson = await pipeClient.SendRequest(Topics.GetSystemConfiguration, "", context.CancellationToken);
        var configurationResult = JsonSerializer.Deserialize(settingsJson, SourceGenerationContext.Default.GetSystemConfigurationResult);

        if (configurationResult == null)
        {
            DeserializeReturnedNull(logger);

            return new GetSystemConfigurationResponse { RequestError = new ErrorInfo(1, "Deserialization failed") };
        }

        if (configurationResult.Configuration is not null)
            responseCache.Set(configurationResult.Configuration);

        if (configurationResult.Status == OperationStatus.Success)
            return new GetSystemConfigurationResponse { Configuration = mapper.ToSuiteFormat(configurationResult.Configuration) };

        if (configurationResult.Status == OperationStatus.Warning)
        {
            WarningStatusReturned(logger, configurationResult.Message);

            return new GetSystemConfigurationResponse { Configuration = mapper.ToSuiteFormat(configurationResult.Configuration) };
        }

        // OperationStatus.Error
        ErrorStatusReturned(logger, configurationResult.Message);

        return new GetSystemConfigurationResponse { RequestError = new ErrorInfo(3, configurationResult.Message) };
    }

    protected override Task<GetSystemConfigurationResponse> HandleException(ConsumeContext<GetSystemConfiguration> context,
        Exception e)
    {
        ExceptionOccurred(logger, e);

        return Task.FromResult(new GetSystemConfigurationResponse { RequestError = new ErrorInfo(4, e.Message) });
    }
}
