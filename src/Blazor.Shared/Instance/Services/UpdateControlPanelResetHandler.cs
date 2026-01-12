using Core.Shared.HostManagement;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using ViciOne.Ui.Blazor.Components.ComboBox;

namespace Blazor.Shared.Instance.Services;

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

            var availableVersions = response.Versions.ToHashSet();
            var currentVersion = state.GetCurrentVersion();

            if (availableVersions.All(k => k != currentVersion))
                availableVersions.Add(currentVersion);

            state.VersionComboBoxItems = [.. availableVersions
                .OrderBy(availableVersion => availableVersion)
                .Select(availableVersion => new ComboBoxItem<string?, string> { Value = availableVersion, Text = availableVersion })];

            state.FetchAvailableVersionsError = null;
        }
        catch (Exception ex)
        {
            state.FetchAvailableVersionsError = ControlPanels.Localization.UpdateControlPanel.FetchAvailableVersionsError;

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
