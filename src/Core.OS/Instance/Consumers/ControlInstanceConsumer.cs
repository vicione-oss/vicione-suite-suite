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
    private readonly ILogger<ControlInstanceConsumer> _logger;
    private readonly IFileSystem _fileSystem;

    public ControlInstanceConsumer(IServiceProvider services, IFileSystem fileSystem, ILogger<ControlInstanceConsumer> logger)
    {
        _services = services;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ControlInstance> context)
    {
        var correlationId = context.Message.CorrelationId;
        var instanceId = context.Message.InstanceId;

        LogConsumingControlInstance(_logger, correlationId, context.Message.Action, instanceId);

        var localProvider = _services.GetRequiredService<ILocalInstanceInformationProvider>();
        var applicationDb = _services.GetRequiredService<IApplicationDbContext>();

        var targetInstance = await applicationDb.InstanceInfo.SingleOrDefaultAsync(k => k.Id == instanceId);
        if (targetInstance is null)
        {
            LogInstanceNotPartOfSystem(_logger, correlationId, instanceId);
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
                    await HandleDeleteInstance(context, applicationDb, localProvider.Local, targetInstance);
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
            LogFailedToProcessCommand(_logger, e, correlationId, context.Message.Action, instanceId);

            var message = new ControlInstanceCompleted(instanceId, context.Message.Action, new ErrorInfo(100, e.Message))
            {
                CorrelationId = correlationId
            };

            await context.Publish(message, context.CancellationToken);
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

    private async Task HandleDeleteInstance(ConsumeContext<ControlInstance> context, IApplicationDbContext dbContext, IInstanceInformation localInstance, InstanceInformation instanceInfo)
    {
        if (localInstance.Type != InstanceType.Master)
        {
            LogInstanceCanOnlyBeRemovedByMaster(_logger, context.Message.InstanceId);
            return;
        }

        // todo: removing an instance from the system can have impact to the cluster. actions need to be defined
        dbContext.InstanceInfo.Remove(instanceInfo);
        await dbContext.SaveChangesAsync(context.CancellationToken);

        await context.Publish(new ControlInstanceCompleted(instanceInfo.Id, InstanceCommand.Delete), context.CancellationToken);
    }

    private Task HandleRestartInstance(ConsumeContext<ControlInstance> context, InstanceInformation instanceInfo)
    {
        EnsureSuiteRestartFile();

        var delay = context.Message.Delay ?? TimeSpan.Zero;
        var instanceId = instanceInfo.Id;
        var correlationId = context.Message.CorrelationId;

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
                LogFailedToProcessCommand(_logger, e, correlationId, InstanceCommand.Restart, instanceId);
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

        var message = new ControlInstanceCompleted(instanceId, InstanceCommand.Synchronize, new ErrorInfo(2, errors))
        {
            CorrelationId = context.CorrelationId ?? Guid.NewGuid()
        };

        return context.Publish(message, context.CancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Consume control instance='{InstanceId}' command='{Command}' correlated by {CorrelationId}")]
    private static partial void LogConsumingControlInstance(ILogger<ControlInstanceConsumer> logger, Guid correlationId, InstanceCommand command, Guid instanceId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Instance='{InstanceId}' is not part of the current system setup correlated by {CorrelationId}")]
    private static partial void LogInstanceNotPartOfSystem(ILogger<ControlInstanceConsumer> logger, Guid correlationId, Guid instanceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to control instance='{InstanceId}' processing '{Command}' correlated by {CorrelationId}")]
    private static partial void LogFailedToProcessCommand(ILogger<ControlInstanceConsumer> logger, Exception exception, Guid correlationId, InstanceCommand command, Guid instanceId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Local instance {InstanceId} is not a valid synchronization orchestrator (master)")]
    private static partial void LogLocalInstanceNotValidSyncOrchestrator(ILogger<ControlInstanceConsumer> logger, Guid instanceId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Instance {InstanceId} is not a valid target system for synchronization")]
    private static partial void LogInstanceNotValidSyncTarget(ILogger<ControlInstanceConsumer> logger, Guid instanceId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Added sync activity instanceId='{InstanceId}' context='{Schema}' table='{Table}'")]
    private static partial void LogAddedSyncActivity(ILogger<ControlInstanceConsumer> logger, Guid instanceId, string schema, string table);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Instance='{InstanceId}' can only be removed by master")]
    private static partial void LogInstanceCanOnlyBeRemovedByMaster(ILogger<ControlInstanceConsumer> logger, Guid instanceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Instance='{InstanceId}' successfully synchronized")]
    private static partial void LogInstanceSuccessfullySynchronized(ILogger<ControlInstanceConsumer> logger, Guid instanceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "One or more errors occured when synchronizing data for instance='{InstanceId}'.{NewLine}{ErrorList}")]
    private static partial void LogSynchronizationErrors(ILogger<ControlInstanceConsumer> logger, Guid instanceId, string newLine, string errorList);
}
