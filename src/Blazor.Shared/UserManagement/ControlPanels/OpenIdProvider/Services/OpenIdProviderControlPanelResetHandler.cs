using Core.Shared.UserManagement.Requests;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.UserManagement.ControlPanels.OpenIdProvider.Services;

internal sealed partial class OpenIdProviderControlPanelResetHandler(IUiMediator mediator,
    ILogger<OpenIdProviderControlPanelResetHandler> logger)
    : IControlPanelResetHandler<OpenIdProviderControlPanelState>
{
    public async Task Reset(OpenIdProviderControlPanelState state, CancellationToken cancellationToken)
    {
        try
        {
            state.BeginLoading();

            var response = await mediator.Request<GetExternalIdProvider, GetExternalIdProviderResponse>(new(),
                cancellationToken);

            if (response.RequestError is not null)
            {
                LeaveUnloaded(state, response.RequestError.Message);

                return;
            }

            Initialize(state, response);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogLoadingProviderFailed(logger, ex);

            LeaveUnloaded(state, null);
        }
        finally
        {
            state.EndLoading();
        }
    }

    private static void Initialize(OpenIdProviderControlPanelState state, GetExternalIdProviderResponse response)
    {
        state.LoadError = null;
        state.Authority = response.Authority;
        state.ClientId = response.ClientId;
        state.ClientSecretStored = response.ClientSecretStored;
        state.ClientSecret = string.Empty;
        state.RemoveStoredClientSecret = false;
    }

    private static void LeaveUnloaded(OpenIdProviderControlPanelState state, string? message)
    {
        state.LoadError = string.IsNullOrWhiteSpace(message) ? CommonPhrases.AnUnknownErrorOccurred : message;
        state.Authority = string.Empty;
        state.ClientId = string.Empty;
        state.ClientSecret = string.Empty;
        state.ClientSecretStored = false;
        state.RemoveStoredClientSecret = false;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Loading the OpenID provider failed")]
    private static partial void LogLoadingProviderFailed(ILogger logger, Exception exception);
}
