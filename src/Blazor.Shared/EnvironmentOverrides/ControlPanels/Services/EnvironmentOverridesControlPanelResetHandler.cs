using Blazor.Shared.EnvironmentOverrides.ControlPanels.Extensions;
using Core.Shared.EnvironmentOverrides.Requests;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Instance;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.EnvironmentOverrides.ControlPanels.Services;

internal sealed partial class EnvironmentOverridesControlPanelResetHandler(IUiMediator mediator,
    IInstanceInformationProvider instanceInformationProvider,
    ILogger<EnvironmentOverridesControlPanelResetHandler> logger)
    : IControlPanelResetHandler<EnvironmentOverridesControlPanelState>
{
    public async Task Reset(EnvironmentOverridesControlPanelState state, CancellationToken cancellationToken)
    {
        state.RestartRequired = false;

        try
        {
            state.BeginLoading();
            var response
                = await mediator.Request<GetEnvironmentOverrides, GetEnvironmentOverridesResponse>(new(),
                    instanceInformationProvider.Local.Id,
                    cancellationToken);

            if (response.RequestError is not null)
            {
                LeaveUnloaded(state, response.RequestError.Message);

                return;
            }

            InitializeOverrides(state, response.Overrides);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogLoadingOverridesFailed(logger, ex);

            LeaveUnloaded(state, null);
        }
        finally
        {
            state.EndLoading();
        }
    }

    private static void InitializeOverrides(EnvironmentOverridesControlPanelState state, IReadOnlyDictionary<string, string> overrides)
    {
        state.LoadError = null;
        state.Initialize(overrides);
    }

    private static void LeaveUnloaded(EnvironmentOverridesControlPanelState state, string? message)
    {
        state.LoadError = string.IsNullOrWhiteSpace(message) ? CommonPhrases.AnUnknownErrorOccurred : message;
        state.Entries = [];
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Loading the environment overrides failed")]
    private static partial void LogLoadingOverridesFailed(ILogger logger, Exception exception);
}
