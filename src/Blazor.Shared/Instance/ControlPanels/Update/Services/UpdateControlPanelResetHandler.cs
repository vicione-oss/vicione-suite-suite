using Core.Shared.HostManagement;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Instance.ControlPanels.Update.Services;

internal sealed partial class UpdateControlPanelResetHandler(IUiMediator mediator, ILogger<UpdateControlPanelResetHandler> logger)
    : IControlPanelResetHandler<UpdateControlPanelState>
{
    public async Task Reset(UpdateControlPanelState state, CancellationToken cancellationToken)
    {
        await FetchAvailableSuiteVersions(state, cancellationToken);

        state.PreselectCurrentVersion();

        state.FlashDeviceEnabled = false;
        state.SwuFilename = null;
        state.SwuFileUploadTicket = null;
        state.SwuFilenameUploaded = null;
        state.SwuFilenameChangedBannerVisible = false;
    }

    private async Task FetchAvailableSuiteVersions(UpdateControlPanelState state, CancellationToken cancellationToken)
    {
        state.FetchingVersions = true;

        state.BeginLoading();
        try
        {
            var response = await mediator.Request<GetAvailableSuiteVersions, GetAvailableSuiteVersionsResponse>(
                new GetAvailableSuiteVersions(), cancellationToken);

            state.SuiteVersions = [.. response.Versions];

            state.FetchAvailableVersionsError = response.RequestError?.Message;
        }
        catch (Exception ex)
        {
            state.FetchAvailableVersionsError = Localization.UpdateControlPanel.FetchAvailableVersionsError;

            FetchingAvailableVersionsFailed(logger, ex);
        }
        finally
        {
            state.FetchingVersions = false;

            state.EndLoading();
        }
    }

    [LoggerMessage(1, LogLevel.Error, "Fetching available versions failed")]
    private static partial void FetchingAvailableVersionsFailed(ILogger logger, Exception exception);
}
