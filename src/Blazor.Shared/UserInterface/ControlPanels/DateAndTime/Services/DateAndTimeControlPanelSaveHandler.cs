using System.Collections.Concurrent;
using Blazor.Shared.Extensions;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.DateAndTime.Services;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Events;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Sdk.Utils;

namespace Blazor.Shared.UserInterface.ControlPanels.DateAndTime.Services;

internal sealed class DateAndTimeControlPanelSaveHandler : IControlPanelSaveHandler<DateAndTimeControlPanelState>,
            IEventConsumer<CrossInstanceConfigurationChanged>,
            IEventConsumer<CrossInstanceConfigurationError>,
            IDisposable
{
    private readonly ITimeZoneDescriptorProvider _timeZoneDescriptorProvider;
    private readonly IClientTimeProvider _timeProvider;
    private readonly IUiMediator _mediator;
    private readonly AutoDisposeList<IDisposable> _subscriptions = [];
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ErrorInfo?>> _taskCompletionSourceMap = new();

    public DateAndTimeControlPanelSaveHandler(ITimeZoneDescriptorProvider timeZoneDescriptorProvider, IClientTimeProvider timeProvider, IUiMediator mediator)
    {
        _timeZoneDescriptorProvider = timeZoneDescriptorProvider;
        _timeProvider = timeProvider;
        _mediator = mediator;
        _subscriptions.Add(_mediator.Register<CrossInstanceConfigurationChanged>(this));
        _subscriptions.Add(_mediator.Register<CrossInstanceConfigurationError>(this));
    }

    public async Task<ISaveResult> Save(DateAndTimeControlPanelState state, CancellationToken cancellationToken)
    {
        var timeZoneDescriptor = await _timeZoneDescriptorProvider.GetTimeZoneDescriptor(state.SelectedTimeZoneId, cancellationToken);
        if (timeZoneDescriptor is null)
            return new SaveErrorResult(Settings.DateAndTime.Localization.ErrorMessages.CannotFindTimeZoneDescriptor);

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneDescriptor.Value.TimeZoneId, out _))
            return new SaveErrorResult(Settings.DateAndTime.Localization.ErrorMessages.CannotFindTimeZoneInfo);

        var command = new SetCrossInstanceConfiguration(null, state.SelectedTimeZoneId);

        var taskCompletionSource = new TaskCompletionSource<ErrorInfo?>();
        _taskCompletionSourceMap[command.CorrelationId] = taskCompletionSource;

        try
        {
            await _mediator.Send(command, cancellationToken);

            var saveResult = await taskCompletionSource.WaitForCommandCompletion(cancellationToken);
            await _timeProvider.Initialize(cancellationToken);

            return saveResult;
        }
        finally
        {
            _taskCompletionSourceMap.TryRemove(command.CorrelationId, out _);
        }
    }

    public Task Consume(ClientContext<CrossInstanceConfigurationChanged> context, CancellationToken cancellationToken)
    {
        if (_taskCompletionSourceMap.TryRemove(context.Message.CorrelationId, out var taskCompletionSource))
            taskCompletionSource.SetResult(null);

        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<CrossInstanceConfigurationError> context, CancellationToken cancellationToken = default)
    {
        if (_taskCompletionSourceMap.TryRemove(context.Message.CorrelationId, out var taskCompletionSource))
            taskCompletionSource.SetResult(context.Message.Error);

        return Task.CompletedTask;
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
}
