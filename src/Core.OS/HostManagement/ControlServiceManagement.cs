using System.Diagnostics;
using Core.OS.HostManagement.Consumers;
using Core.OS.HostManagement.Extensions;
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

public class ControlServiceManagement(IPipeClient pipeClient, SystemConfigurationCache responseCache, ILogger<ControlServiceManagement> logger) : IControlServiceManagement
{
    public bool IsAvailable => pipeClient is not MockPipeClient;

    public async Task<ControlServiceManagementResult> TryControlService(ServiceCommand command, string serviceName, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            var errorInfo = new ErrorInfo(ControlServiceError.ControlServiceUnavailable, "Operation not supported. HMS not installed or connected.");
            return new ControlServiceManagementResult(serviceName, SdkServiceState.Unknown, errorInfo);
        }

        try
        {
            if (command is ServiceCommand.Enable or ServiceCommand.Disable)
                return await ChangeServiceConfiguration(command, serviceName, cancellationToken);
            return await ChangeServiceState(command, serviceName, cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to {Command} service '{ServiceName}'.", command, serviceName);
            var errorInfo = new ErrorInfo(ControlServiceError.UnknownError, e.Message);
            return new ControlServiceManagementResult(serviceName, SdkServiceState.Unknown, errorInfo);
        }
    }

    private async Task<ControlServiceManagementResult> ChangeServiceConfiguration(ServiceCommand command, string serviceName, CancellationToken cancellationToken)
    {
        var response = await GetHostMgmtSystemConfigurationConsumer
            .FetchSystemConfigurationFromHostManagement(pipeClient, responseCache, cancellationToken);

        if (response.Configuration is null)
            throw new InvalidOperationException("Failed to fetch system configuration.");

        // check for existing ones or add new one
        var serviceToControl = GetOrAddServiceToControl(response.Configuration, serviceName);

        ApplyServiceCommand(serviceToControl, command);

        var setResult = await pipeClient.SetSystemConfiguration(response.Configuration, cancellationToken);
        if (setResult?.Status == OperationStatus.Success)
            return new ControlServiceManagementResult(serviceName, ToSuiteState(command));

        var errorCode = (int?)setResult?.Status ?? ControlServiceError.UnknownError;
        var errorInfo = new ErrorInfo(errorCode, $"{command} service '{serviceName}' failed with {errorCode}. {setResult?.Message}");
        logger.LogError("{Command} service '{ServiceName}' failed with {ErrorCode}. {Message}", command, serviceName, errorCode, setResult?.Message);
        return new ControlServiceManagementResult(serviceName, ToSuiteState(command), errorInfo);
    }

    private static ServiceDetail GetOrAddServiceToControl(SystemConfiguration? configuration, string serviceName)
    {
        // we have no configuration at all so no services!
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
        ControlServiceManagementResult result = new(serviceName, SdkServiceState.Unknown);
        if (command is ServiceCommand.Stop or ServiceCommand.Restart)
        {
            var requestResult = await pipeClient.StopService(serviceName, cancellationToken);
            result = HandleServiceControlResult(requestResult, command, serviceName);
        }

        if (!result.Success)
            return result;

        if (command is ServiceCommand.Start or ServiceCommand.Restart)
        {
            var requestResult = await pipeClient.StartService(serviceName, cancellationToken);
            return HandleServiceControlResult(requestResult, command, serviceName);
        }

        return result;
    }

    private ControlServiceManagementResult HandleServiceControlResult(ServiceControlResult? setResult, ServiceCommand command, string serviceName)
    {
        if (setResult?.Status != OperationStatus.Success)
        {
            var errorCode = (int?)setResult?.Status ?? ControlServiceError.UnknownError;
            var errorInfo = new ErrorInfo(errorCode, $"{command} service '{serviceName}' failed with {errorCode}. {setResult?.Message}");
            logger.LogError("{Command} service '{ServiceName}' failed with {ErrorCode}. {Message}", command, serviceName, errorCode, setResult?.Message);
            return new ControlServiceManagementResult(serviceName, ToSuiteState(command), errorInfo);
        }

        return new ControlServiceManagementResult(serviceName, ToSuiteState(command));
    }
}
