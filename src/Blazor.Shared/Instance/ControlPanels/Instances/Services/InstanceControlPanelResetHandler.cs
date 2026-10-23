using Blazor.Shared.Instance.ControlPanels.Instances.Models;
using Blazor.Shared.Services;
using Core.Shared.Instance.Requests;
using Core.Shared.Mail.Requests;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Instance.ControlPanels.Instances.Services;

internal partial class InstanceControlPanelResetHandler(
    IUiMediator mediator,
    IBackendLogService logService,
    ILogger<InstanceControlPanelResetHandler> logger)
    : IControlPanelResetHandler<InstanceControlPanelState>, IDisposable
{
    private CancellationTokenSource? _smtpSettingsLoadCancellationTokenSource;

    public void Dispose()
    {
        var cancellationTokenSource = Interlocked.Exchange(ref _smtpSettingsLoadCancellationTokenSource, null);
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
    }

    public async Task Reset(InstanceControlPanelState state, CancellationToken cancellationToken)
    {
        if (state.InstanceId is null)
            return;

        // Not awaited: the SMTP settings come from the instance itself, so an unreachable instance must only delay their
        // own page, never the instance information, which the node serving the UI answers from its database.
        _ = LoadSmtpSettings(state, state.InstanceId.Value);

        state.BeginLoading();
        try
        {
            var response = await mediator.Request<GetInstances, GetInstancesResponse>(new(state.InstanceId), cancellationToken);
            var instance = response.Instances.FirstOrDefault();
            if (instance is null)
            {
                state.InstanceInformation = null;

                return;
            }

            state.InstanceInformation = new InstanceInformationModel(instance);
            state.LogLevel = await logService.GetLogLevel();
        }
        finally
        {
            state.EndLoading();
        }
    }

    private async Task LoadSmtpSettings(InstanceControlPanelState state, Guid instanceId)
    {
        // The reset token is cancelled by the next save of the panel, which would leave this page loading forever, so only a
        // superseding load or the end of the scope cancels this request. Whoever takes the source out of the field disposes it.
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var supersededCancellationTokenSource = Interlocked.Exchange(ref _smtpSettingsLoadCancellationTokenSource, cancellationTokenSource);

        // Begins before the first await, so the panel never renders without the load in progress.
        state.BeginSmtpSettingsLoad();
        try
        {
            if (supersededCancellationTokenSource is not null)
            {
                await supersededCancellationTokenSource.CancelAsync();
                supersededCancellationTokenSource.Dispose();
            }

            var response = await mediator.Request<GetSmtpSettings, GetSmtpSettingsResponse>(new(), instanceId, cancellationToken);

            if (response.RequestError is not null)
                LogSmtpSettingsRequestFailed(logger, instanceId, response.RequestError.ErrorCode, response.RequestError.Message);

            // A response that arrived just before the cancellation still completes the request; the superseding load owns the
            // page state.
            if (!cancellationToken.IsCancellationRequested)
                state.CompleteSmtpSettingsLoad(response.Settings, failed: response.RequestError is not null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Only a superseding load or the end of the scope cancels; the superseding load owns the page state.
        }
        catch (Exception ex)
        {
            // Runs unobserved, so every failure, a request timeout included, has to end up on the page instead of being lost.
            LogSmtpSettingsLoadFailed(logger, instanceId, ex);

            if (!cancellationToken.IsCancellationRequested)
                state.CompleteSmtpSettingsLoad(smtpSettings: null, failed: true);
        }
        finally
        {
            if (Interlocked.CompareExchange(ref _smtpSettingsLoadCancellationTokenSource, null, cancellationTokenSource) == cancellationTokenSource)
                cancellationTokenSource.Dispose();
        }
    }

    [LoggerMessage(LogLevel.Warning, "Instance '{InstanceId}' answered the SMTP settings request with error {Code}: {Message}")]
    private static partial void LogSmtpSettingsRequestFailed(ILogger logger, Guid instanceId, int code, string? message);

    [LoggerMessage(LogLevel.Error, "Loading the SMTP settings of instance '{InstanceId}' failed")]
    private static partial void LogSmtpSettingsLoadFailed(ILogger logger, Guid instanceId, Exception exception);
}
