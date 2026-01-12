using System.Collections.Concurrent;
using Blazor.Shared.Extensions;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Events;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Sdk.Utils;

namespace Blazor.Shared.UserInterface.ControlPanels.Language.Services;

internal sealed class LanguageControlPanelSaveHandler : IControlPanelSaveHandler<LanguageControlPanelState>,
    IEventConsumer<CrossInstanceConfigurationChanged>,
    IEventConsumer<CrossInstanceConfigurationError>,
    IDisposable
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ErrorInfo?>> _taskCompletionSourceMap = new();
    private readonly IUiMediator _mediator;
    private readonly AutoDisposeList<IDisposable> _subscriptions = [];

    public LanguageControlPanelSaveHandler(IUiMediator mediator)
    {
        _mediator = mediator;

        _subscriptions.Add(_mediator.Register<CrossInstanceConfigurationChanged>(this));
        _subscriptions.Add(_mediator.Register<CrossInstanceConfigurationError>(this));
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

    public async Task<ISaveResult> Save(LanguageControlPanelState state, CancellationToken cancellationToken)
    {
        if (state.CrossInstanceConfiguration is null)
            throw new InvalidOperationException(Localization.LanguageControlPanelSaveHandler.NoApplicationConfigurationFound);

        state.CrossInstanceConfiguration.CultureName = state.SelectedCulture.Name;

        var command = new SetCrossInstanceConfiguration(state.CrossInstanceConfiguration.CultureName, state.CrossInstanceConfiguration.TimeZoneId);

        var taskCompletionSource = new TaskCompletionSource<ErrorInfo?>();
        _taskCompletionSourceMap[command.CorrelationId] = taskCompletionSource;

        try
        {
            await _mediator.Send(command, cancellationToken);

            var saveResult = await taskCompletionSource.WaitForCommandCompletion(cancellationToken);

            state.ShowLanguageSavedBanner = true;
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
}
