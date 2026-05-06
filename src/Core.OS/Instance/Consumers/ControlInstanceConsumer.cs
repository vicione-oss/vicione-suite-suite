using System.IO.Abstractions;
using Core.OS.DbContext;
using Core.OS.HostManagement;
using Core.OS.HostManagement.Extensions;
using Core.OS.Instance.Initialization;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Events;
using HostManagement.Shared.Communication.Enums;
using MassTransit;
using MassTransit.Courier.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sdk.Backend.Extensions;
using Sdk.Backend.Messaging;
using Sdk.Instance;
using Sdk.Messaging;

namespace Core.OS.Instance.Consumers;

[ReadOnlyConsumer]
public sealed partial class ControlInstanceConsumer : TrackingConsumerBase, IConsumer<ControlInstance>
{
    private readonly IServiceProvider _services;
    private readonly IApplicationDbContext _applicationDb;
    private readonly ILogger<ControlInstanceConsumer> _logger;
    private readonly IFileSystem _fileSystem;

    public ControlInstanceConsumer(IServiceProvider services, ILogger<ControlInstanceConsumer> logger, IFileSystem fileSystem)
    {
        _services = services;
        _logger = logger;
        _fileSystem = fileSystem;
        _applicationDb = _services.GetRequiredService<IApplicationDbContext>();
    }

    public async Task Consume(ConsumeContext<ControlInstance> context)
    {
        LogConsumingControlInstance(_logger, nameof(ControlInstance), context.Message.Action, context.Message.InstanceId);

        var localProvider = _services.GetRequiredService<ILocalInstanceInformationProvider>();
        var targetInstance = await _applicationDb.InstanceInfo.SingleOrDefaultAsync(k => k.Id == context.Message.InstanceId);
        if (targetInstance is null)
        {
            LogInstanceNotPartOfSystem(_logger, context.Message.InstanceId);
            return;
        }

        try
        {
            switch (context.Message.Action)
            {
                case InstanceCommand.Synchronize:
                    await HandleResyncInstance(context, localProvider.Local, targetInstance);
                    break;

                case InstanceCommand.Delete:
                    await HandleDeleteInstance(context, localProvider.Local, targetInstance);
                    break;

                case InstanceCommand.Restart:
                    await HandleRestartInstance(context, targetInstance);
                    break;

                default:
                    throw new ArgumentOutOfRangeException($"Unknown action requested '{context.Message.Action}'");
            }
        }
        catch (Exception e)
        {
            LogFailedToProcessCommand(_logger, e, context.Message.Action, context.Message.InstanceId);

            await context.Publish(new ControlInstanceError(context.Message.InstanceId, context.Message.Action, new(100, e.Message)), context.CancellationToken);
        }
    }

    private async Task HandleResyncInstance(ConsumeContext<ControlInstance> context, IInstanceInformation localInstance, InstanceInformation instanceInfo)
    {
        if (localInstance.Type != InstanceType.Master)
        {
            LogLocalInstanceNotValidSyncOrchestrator(_logger, context.Message.InstanceId);
            return;
        }

        if (instanceInfo.Type != InstanceType.Slave)
        {
            LogInstanceNotValidSyncTarget(_logger, context.Message.InstanceId);
            return;
        }

        var factory = _services.GetRequiredService<IRoutingSlipBuilderFactory>();
        var builder = factory.Create(context.CorrelationId ?? Guid.NewGuid());

        var arguments = await SyncDataHelpers.CreateSyncDataArgumentsPg(_services, instanceInfo.InstalledModules, context.CancellationToken);
        foreach (var argument in arguments)
        {
            builder.AddActivity<SyncDataActivity, SyncDataArguments>(argument, instanceInfo.Id);
            LogAddedSyncActivity(_logger, instanceInfo.Id, argument.DbContextTypeName, argument.Table);
        }

        builder.AddVariable(SyncDataHelpers.InstanceIdVariableKey, context.Message.InstanceId);
        await ExecuteTracked(context, builder);
    }

    private async Task HandleDeleteInstance(ConsumeContext<ControlInstance> context, IInstanceInformation localInstance, InstanceInformation instanceInfo)
    {
        if (localInstance.Type != InstanceType.Master)
        {
            LogInstanceCanOnlyBeRemovedByMaster(_logger, context.Message.InstanceId);
            return;
        }

        // todo: removing an instance from the system can have impact to the cluster. actions need to be defined
        _applicationDb.InstanceInfo.Remove(instanceInfo);
        await _applicationDb.SaveChangesAsync(context.CancellationToken);

        await context.Publish(new ControlInstanceCompleted(instanceInfo.Id, InstanceCommand.Delete), context.CancellationToken);
    }

    private Task HandleRestartInstance(ConsumeContext<ControlInstance> context, InstanceInformation instanceInfo)
    {
        EnsureSuiteRestartFile();

        var delay = context.Message.Delay ?? TimeSpan.Zero;
        var instanceId = instanceInfo.Id;

        // Capture a root/long-lived service provider to create a fresh scope after the delay
        var rootServices = _services.GetRequiredService<IServiceScopeFactory>();

        _ = Task.Run(async () =>
        {
            try
            {
                // CancellationToken from the original context is intentionally not passed to Delay or the pipe call
                // —> the token would be cancelled when the consumer completes
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay);

                await using var scope = rootServices.CreateAsyncScope();
                var pipeClient = scope.ServiceProvider.GetRequiredService<IPipeClient>();
                var options = scope.ServiceProvider.GetRequiredService<IOptions<InstanceOptions>>();

                var result = await pipeClient.RestartService(options.Value.ServiceName)
                    ?? throw new InvalidOperationException("Restart request result from host management is null.");

                if (result.Status == OperationStatus.Error)
                    throw new InvalidOperationException(result.Message);

                // IPublishEndpoint is used instead of ConsumeContext since the consume context won't be valid after the consumer returns
                var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
                await publishEndpoint.Publish(new ControlInstanceCompleted(instanceId, InstanceCommand.Restart));
            }
            catch (OperationCanceledException)
            {
                // successful restart leads to service cancellation — expected
            }
            catch (Exception e)
            {
                LogFailedToProcessCommand(_logger, e, InstanceCommand.Restart, instanceId);
            }
        });

        return Task.CompletedTask;
    }

    private void EnsureSuiteRestartFile()
    {
        var runtimeDirectory = Environment.GetEnvironmentVariable("RUNTIME_DIRECTORY") ?? "/run/vicione-suite";
        _fileSystem.Directory.CreateDirectory(runtimeDirectory);
        var filePath = _fileSystem.Path.Combine(runtimeDirectory, "suite-ui-restart");
        if (!_fileSystem.File.Exists(filePath))
            _fileSystem.File.Create(filePath, 0, FileOptions.None).Dispose();
    }

    protected override Task ConsumeCompleted(ConsumeContext<RoutingSlipCompleted> context)
    {
        var instanceId = context.Message.GetVariable<Guid>(SyncDataHelpers.InstanceIdVariableKey);
        LogInstanceSuccessfullySynchronized(_logger, instanceId);
        return context.Publish(new ControlInstanceCompleted(instanceId, InstanceCommand.Synchronize),
            context.CancellationToken);
    }

    protected override Task ConsumeFaulted(ConsumeContext<RoutingSlipFaulted> context)
    {
        var instanceId = context.Message.GetVariable<Guid>(SyncDataHelpers.InstanceIdVariableKey);
        var errorList = context.Message.ActivityExceptions.Select(ex => ex.ExceptionInfo.Message).ToList();
        var errors = string.Join(Environment.NewLine, errorList);
        LogSynchronizationErrors(_logger, instanceId, Environment.NewLine, errors);

        var message = new ControlInstanceError(instanceId, InstanceCommand.Synchronize, new ErrorInfo(2, errors));

        return context.Publish(message, context.CancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Consume {command} action:{action} for instance {instanceId}")]
    private static partial void LogConsumingControlInstance(ILogger<ControlInstanceConsumer> logger, string command, InstanceCommand action, Guid instanceId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Instance {instanceId} is not part of the current system setup")]
    private static partial void LogInstanceNotPartOfSystem(ILogger<ControlInstanceConsumer> logger, Guid instanceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to process '{command}' on instance='{instanceId}'")]
    private static partial void LogFailedToProcessCommand(ILogger<ControlInstanceConsumer> logger, Exception exception, InstanceCommand command, Guid instanceId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Local instance {instanceId} is not a valid synchronization orchestrator (master)")]
    private static partial void LogLocalInstanceNotValidSyncOrchestrator(ILogger<ControlInstanceConsumer> logger, Guid instanceId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Instance {instanceId} is not a valid target system for synchronization")]
    private static partial void LogInstanceNotValidSyncTarget(ILogger<ControlInstanceConsumer> logger, Guid instanceId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Added sync activity instanceId:{instanceId} context:{schema} table:{table}")]
    private static partial void LogAddedSyncActivity(ILogger<ControlInstanceConsumer> logger, Guid instanceId, string schema, string table);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Instance {instanceId} can only be removed by master")]
    private static partial void LogInstanceCanOnlyBeRemovedByMaster(ILogger<ControlInstanceConsumer> logger, Guid instanceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Instance {instanceId} successfully synchronized")]
    private static partial void LogInstanceSuccessfullySynchronized(ILogger<ControlInstanceConsumer> logger, Guid instanceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "One or more errors occured when synchronizing data for instance {instanceId}:{newLine}{errorList}")]
    private static partial void LogSynchronizationErrors(ILogger<ControlInstanceConsumer> logger, Guid instanceId, string newLine, string errorList);
}
