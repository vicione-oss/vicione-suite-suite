using System.Collections.Concurrent;
using Blazor.Shared.Extensions;
using Blazor.Shared.Network.ControlPanels;
using Blazor.Shared.Network.Models;
using Core.Shared.HostManagement.Commands;
using Core.Shared.HostManagement.Events;
using Core.Shared.HostManagement.Services;
using HostManagement.Shared.Validation;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Sdk.SystemConfiguration.Events;
using Sdk.Utils;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Network.Services;

internal abstract partial class NetworkControlPanelSaveHandlerBase<TState>
    : IControlPanelSaveHandler<TState>, IEventConsumer<SystemConfigurationChanged>, IEventConsumer<SetSystemConfigurationError>, IDisposable
        where TState : NetworkControlPanelStateBase
{
    private readonly IUiMediator _mediator;
    private readonly ILogger _logger;

    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ErrorInfo?>> _taskCompletionSourceMap = new();
    private readonly AutoDisposeList<IDisposable> _subscriptionHandles = [];

    internal ISystemConfigurationService SystemConfigurationService { get; }

    public NetworkControlPanelSaveHandlerBase(IUiMediator mediator, ISystemConfigurationService systemConfigurationService, ILogger logger)
    {
        _mediator = mediator;
        SystemConfigurationService = systemConfigurationService;
        _logger = logger;

        _subscriptionHandles.Add(mediator.Register<SystemConfigurationChanged>(this));
        _subscriptionHandles.Add(mediator.Register<SetSystemConfigurationError>(this));
    }

    public void Dispose()
    {
        _subscriptionHandles.Dispose();

        var correlationIds = _taskCompletionSourceMap.Keys;

        foreach (var correlationId in correlationIds)
        {
            if (_taskCompletionSourceMap.TryRemove(correlationId, out var taskCompletionSource))
                taskCompletionSource.SetCanceled();
        }

        _taskCompletionSourceMap.Clear();

        GC.SuppressFinalize(this);
    }

    public async Task<ISaveResult> Save(TState state, CancellationToken cancellationToken)
    {
        SaveInvoked(_logger);

        var saveInternalResult = await SaveInternal(state);
        if (saveInternalResult is not SystemConfigurationSaveInternalResult systemConfigurationSaveInternalResult)
        {
            if (saveInternalResult is SaveInternalErrorResult saveInternalErrorResult)
                return new SaveErrorResult(saveInternalErrorResult.Message, saveInternalErrorResult.ErrorCode);
            else
                return new SaveErrorResult(CommonPhrases.AnUnexpectedErrorOccurred, -1);
        }

        var proposedSystemConfiguration = systemConfigurationSaveInternalResult.ProposedSystemConfiguration;

        var validateResult = await new SystemConfigurationValidator().ValidateAsync(proposedSystemConfiguration, cancellationToken);
        if (!validateResult.IsValid)
            return new SaveErrorResult(string.Join(" ", validateResult.Entries.Select(e => e.Message)));

        var command = new SetSystemConfiguration(proposedSystemConfiguration);

        var taskCompletionSource = new TaskCompletionSource<ErrorInfo?>();
        _taskCompletionSourceMap[command.CorrelationId] = taskCompletionSource;

        try
        {
            await _mediator.Send(command, cancellationToken);

            var result = await taskCompletionSource.WaitForCommandCompletion(cancellationToken);

            if (result is SaveSuccessResult && !taskCompletionSource.Task.IsCanceled)
                await SystemConfigurationService.SetSystemConfiguration(proposedSystemConfiguration);

            return result;
        }
        finally
        {
            _taskCompletionSourceMap.TryRemove(command.CorrelationId, out _);
        }
    }

    protected abstract Task<ISaveInternalResult> SaveInternal(TState state);

    public Task Consume(ClientContext<SystemConfigurationChanged> context, CancellationToken cancellationToken)
    {
        if (_taskCompletionSourceMap.TryRemove(context.Message.CorrelationId, out var taskCompletionSource))
            taskCompletionSource.SetResult(null);

        SetSystemConfigurationSuccess(_logger);

        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<SetSystemConfigurationError> context, CancellationToken cancellationToken)
    {
        if (_taskCompletionSourceMap.TryRemove(context.Message.CorrelationId, out var taskCompletionSource))
            taskCompletionSource.SetResult(context.Message.Error);

        SetSystemConfigurationFailed(_logger, context.Message.Error);

        return Task.CompletedTask;
    }

    [LoggerMessage(1, LogLevel.Information, "Save invoked")]
    private static partial void SaveInvoked(ILogger logger);

    [LoggerMessage(2, LogLevel.Information, "Set system configuration successfully set")]
    private static partial void SetSystemConfigurationSuccess(ILogger logger);

    [LoggerMessage(3, LogLevel.Error, "Set system configuration failed: {error}")]
    private static partial void SetSystemConfigurationFailed(ILogger logger, ErrorInfo error);
}
