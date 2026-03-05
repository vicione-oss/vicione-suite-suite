using Blazor.Shared.Services;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Events;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Instance.ControlPanels.Instances.Services;

internal sealed class InstanceControlPanelSaveHandler : ControlPanelSaveHandlerBase<InstanceControlPanelState>,
    IEventConsumer<InstanceInformationUpdated>
{
    private readonly IBackendLogService _logService;
    private readonly ILogger<InstanceControlPanelSaveHandler> _logger;

    public InstanceControlPanelSaveHandler(IUiMediator mediator, IBackendLogService logService, ILogger<InstanceControlPanelSaveHandler> logger) : base(mediator)
    {
        _logService = logService;
        _logger = logger;

        Register(this);
    }

    public override async Task<ISaveResult> Save(InstanceControlPanelState state, CancellationToken cancellationToken)
    {
        if (state.InstanceInformation is null)
            throw new InvalidOperationException(Localization.InstanceControlPanelSaveHandler.NoInstanceInformationAvailable);

        var command = new UpdateInstanceInformation(state.InstanceInformation);

        return await SendAndWaitForCompletionAfterwards(command, async (_) =>
        {
            if (state.LogLevel.HasValue)
            {
                await _logService.SetLogLevel(state.LogLevel.Value);
                _logger.LogDebug("Log level changed to '{LogLevel}'", state.LogLevel.Value);
            }
        }, cancellationToken);
    }

    public Task Consume(ClientContext<InstanceInformationUpdated> context, CancellationToken cancellationToken)
    {
        var errorInfo = context.Message.Success ? null : new ErrorInfo(0, $"{CommonPhrases.AnUnexpectedErrorOccurred} {CommonPhrases.SeeLogsForFurtherDetails}");
        if (errorInfo is not null)
        {
            CompleteWithError(context.Message.CorrelationId, errorInfo);
        }
        else
        {
            CompleteWithSuccess(context.Message.CorrelationId);
        }

        return Task.CompletedTask;
    }
}
