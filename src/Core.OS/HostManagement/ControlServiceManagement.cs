using System.Diagnostics;
using Core.OS.HostManagement.Consumers;
using Core.OS.HostManagement.Extensions;
using Core.Shared.HostManagement.Extensions;
using HostManagement.Shared.Communication.Contracts;
using HostManagement.Shared.Communication.Enums;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Service;
using HostManagement.Shared.Enums;
using Sdk.Messaging;
using Sdk.SystemConfiguration;
using Sdk.SystemConfiguration.Commands;
using Sdk.SystemConfiguration.Events;
using SdkServiceState = Sdk.SystemConfiguration.Contracts.ServiceState;

namespace Core.OS.HostManagement;

public sealed partial class ControlServiceManagement(IPipeClient pipeClient, SystemConfigurationCache responseCache, ILogger<ControlServiceManagement> logger) : IControlServiceManagement
{
    public bool IsAvailable => !pipeClient.IsMock;

    public async Task<ControlServiceManagementResult> TryControlService(ServiceCommand command, string serviceName, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return Unavailable(serviceName);

        try
        {
            if (command is ServiceCommand.Enable or ServiceCommand.Disable)
                return await ChangeServiceConfiguration(command, serviceName, cancellationToken);

            return await ChangeServiceState(command, serviceName, cancellationToken);
        }
        catch (Exception e)
        {
            LogFailedToControlService(logger, e, command, serviceName);

            if (pipeClient.State is PipeState.NotOpened)
                return Unavailable(serviceName);

            var errorInfo = new ErrorInfo(ControlServiceErrorCodes.UnknownError, e.Message);

            return new ControlServiceManagementResult(serviceName, SdkServiceState.Unknown, errorInfo);
        }
    }

    private static ControlServiceManagementResult Unavailable(string serviceName)
    {
        var errorInfo = new ErrorInfo(ControlServiceErrorCodes.ControlServiceUnavailable, "Operation not supported. HMS not installed or connected.");

        return new ControlServiceManagementResult(serviceName, SdkServiceState.Unknown, errorInfo);
    }

    private async Task<ControlServiceManagementResult> ChangeServiceConfiguration(ServiceCommand command, string serviceName, CancellationToken cancellationToken)
    {
        var response = await GetHostMgmtSystemConfigurationConsumer
            .FetchSystemConfigurationFromHostManagement(pipeClient, responseCache, cancellationToken);

        if (response.Configuration is null)
            throw new InvalidOperationException("Failed to fetch system configuration.");

        // The cached configuration stays untouched, so that a rejected change does not end up in the cache.
        var configuration = response.Configuration.Clone();

        // Existing entries are reused, otherwise a new one is added.
        var serviceToControl = GetOrAddServiceToControl(configuration, serviceName);

        ApplyServiceCommand(serviceToControl, command);

        var setResult = await pipeClient.SetSystemConfiguration(configuration, cancellationToken);
        if (setResult?.Status == OperationStatus.Success)
        {
            // Invalidated before the caller publishes SystemConfigurationChanged, on which the UI reloads (#2933).
            responseCache.Invalidate();

            return new ControlServiceManagementResult(serviceName, ToSuiteState(command));
        }

        var errorCode = (int?)setResult?.Status ?? ControlServiceErrorCodes.UnknownError;
        var errorInfo = new ErrorInfo(errorCode, $"{command} service '{serviceName}' failed with {errorCode}. {setResult?.Message}");
        LogServiceCommandFailed(logger, command, serviceName, errorCode, setResult?.Message);

        return new ControlServiceManagementResult(serviceName, SdkServiceState.Unknown, errorInfo);
    }

    private static ServiceDetail GetOrAddServiceToControl(SystemConfiguration? configuration, string serviceName)
    {
        // No configuration means no services.
        if (configuration is null)
            throw new InvalidOperationException("SystemConfiguration does not exist");

        var serviceToControl = configuration.Services.Find(s => s.Name == serviceName);
        if (serviceToControl is null)
        {
            serviceToControl = new ServiceDetail
            {
                Name = serviceName,
                State = ServiceState.Unknown,
            };

            configuration.Services.Add(serviceToControl);
        }

        return serviceToControl;
    }

    private static void ApplyServiceCommand(ServiceDetail service, ServiceCommand command)
        => service.State = command switch
        {
            ServiceCommand.Enable => ServiceState.Enabled,
            ServiceCommand.Disable => ServiceState.Disabled,
            _ => throw new UnreachableException(),
        };

    private static SdkServiceState ToSuiteState(ServiceCommand state)
        => state switch
        {
            ServiceCommand.Enable => SdkServiceState.Enabled,
            ServiceCommand.Disable => SdkServiceState.Disabled,
            ServiceCommand.Start => SdkServiceState.Enabled,
            ServiceCommand.Stop => SdkServiceState.Disabled,
            ServiceCommand.Restart => SdkServiceState.Enabled,
            _ => SdkServiceState.Unknown,
        };

    private async Task<ControlServiceManagementResult> ChangeServiceState(ServiceCommand command, string serviceName, CancellationToken cancellationToken)
    {
        var requestResult = command switch
        {
            ServiceCommand.Start => await pipeClient.StartService(serviceName, cancellationToken),
            ServiceCommand.Stop => await pipeClient.StopService(serviceName, cancellationToken),
            ServiceCommand.Restart => await pipeClient.RestartService(serviceName, cancellationToken),
            _ => throw new UnreachableException(),
        };

        return HandleServiceControlResult(requestResult, command, serviceName);
    }

    private ControlServiceManagementResult HandleServiceControlResult(ServiceControlResult? setResult, ServiceCommand command, string serviceName)
    {
        if (setResult?.Status != OperationStatus.Success)
        {
            var errorCode = (int?)setResult?.Status ?? ControlServiceErrorCodes.UnknownError;
            var errorInfo = new ErrorInfo(errorCode, $"{command} service '{serviceName}' failed with {errorCode}. {setResult?.Message}");
            LogServiceCommandFailed(logger, command, serviceName, errorCode, setResult?.Message);

            return new ControlServiceManagementResult(serviceName, SdkServiceState.Unknown, errorInfo);
        }

        return new ControlServiceManagementResult(serviceName, ToSuiteState(command));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to {Command} service '{ServiceName}'.")]
    private static partial void LogFailedToControlService(ILogger<ControlServiceManagement> logger, Exception exception, ServiceCommand command, string serviceName);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Command} service '{ServiceName}' failed with {ErrorCode}. {Message}")]
    private static partial void LogServiceCommandFailed(ILogger<ControlServiceManagement> logger, ServiceCommand command, string serviceName, int errorCode, string? message);
}
