using System.Collections.Concurrent;
using Blazor.Shared.Extensions;
using Blazor.Shared.Services;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Events;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Sdk.Utils;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Instance.Services;

internal sealed class InstanceControlPanelSaveHandler : IControlPanelSaveHandler<InstanceControlPanelState>,
    IEventConsumer<InstanceInformationUpdated>,
    IDisposable
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ErrorInfo?>> _taskCompletionSourceMap = new();
    private readonly IUiMediator _mediator;
    private readonly IBackendLogService _logService;
    private readonly ILogger<InstanceControlPanelSaveHandler> _logger;
    private readonly AutoDisposeList<IDisposable> _subscriptions = [];

    public InstanceControlPanelSaveHandler(IUiMediator mediator, IBackendLogService logService, ILogger<InstanceControlPanelSaveHandler> logger)
    {
        _mediator = mediator;
        _logService = logService;
        _logger = logger;
        _subscriptions.Add(_mediator.Register<InstanceInformationUpdated>(this));
    }

    public void Dispose()
    {
        _subscriptions.Dispose();

        var correlationIds = _taskCompletionSourceMap.Keys;

        foreach (var correlationId in correlationIds)
        {
            if (_taskCompletionSourceMap.TryRemove(correlationId, out var taskCompletionSource))
                taskCompletionSource.SetCanceled();
        }

        _taskCompletionSourceMap.Clear();
    }

    public async Task<ISaveResult> Save(InstanceControlPanelState state, CancellationToken cancellationToken)
    {
        if (state.InstanceInformation is null)
            throw new InvalidOperationException(Localization.InstanceControlPanelSaveHandler.NoInstanceInformationAvailable);

        var command = new UpdateInstanceInformation(state.InstanceInformation);

        var taskCompletionSource = new TaskCompletionSource<ErrorInfo?>();
        _taskCompletionSourceMap[command.CorrelationId] = taskCompletionSource;

        try
        {
            await _mediator.Send(command, cancellationToken);

            if (state.LogLevel.HasValue)
            {
                await _logService.SetLogLevel(state.LogLevel.Value);
                _logger.LogDebug("Log level changed to '{LogLevel}'", state.LogLevel.Value);
            }

            return await taskCompletionSource.WaitForCommandCompletion(cancellationToken);
        }
        finally
        {
            _taskCompletionSourceMap.TryRemove(command.CorrelationId, out _);
        }
    }

    public Task Consume(ClientContext<InstanceInformationUpdated> context, CancellationToken cancellationToken)
    {
        var errorInfo = context.Message.Success ? null : new ErrorInfo(0, $"{CommonPhrases.AnUnexpectedErrorOccurred} {CommonPhrases.SeeLogsForFurtherDetails}");

        if (_taskCompletionSourceMap.TryRemove(context.Message.CorrelationId, out var taskCompletionSource))
            taskCompletionSource.SetResult(errorInfo);

        return Task.CompletedTask;
    }
}
