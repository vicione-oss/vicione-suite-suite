using Core.Shared.UserManagement.Commands;
using Core.Shared.UserManagement.Configuration;
using Core.Shared.UserManagement.Events;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.UserManagement.ControlPanels.OpenIdProvider.Services;

internal sealed class OpenIdProviderControlPanelSaveHandler :
    ControlPanelSaveHandlerBase<OpenIdProviderControlPanelState>,
    IEventConsumer<ExternalIdProviderChanged>, IEventConsumer<SetExternalIdProviderError>
{
    public OpenIdProviderControlPanelSaveHandler(IUiMediator mediator) : base(mediator)
    {
        Register<ExternalIdProviderChanged>();
        Register<SetExternalIdProviderError>();
    }

    public override async Task<ISaveResult> Save(OpenIdProviderControlPanelState state,
        CancellationToken cancellationToken)
    {
        if (state.LoadError is not null)
            return new SaveErrorResult(Localization.OpenIdProviderControlPanel.SaveBlockedByLoadFailureError);

        var authority = state.Authority.Trim();
        var clientId = state.ClientId.Trim();

        var validationError = Validate(authority, clientId);
        if (validationError is not null)
            return new SaveErrorResult(validationError);

        var secretUpdate = ResolveSecretUpdate(state, removal: authority.Length == 0);

        var command = new SetExternalIdProvider
        {
            Authority = authority,
            ClientId = clientId,
            ClientSecret = secretUpdate
        };

        var result = await SendAndWaitForCompletion(command, cancellationToken);

        if (result is SaveSuccessResult)
            ApplySaved(state, authority, clientId, secretUpdate);

        return result;
    }

    public Task Consume(ClientContext<ExternalIdProviderChanged> context, CancellationToken cancellationToken)
    {
        CompleteWithSuccess(context.Message.CorrelationId);

        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<SetExternalIdProviderError> context, CancellationToken cancellationToken)
    {
        CompleteWithError(context.Message.CorrelationId, context.Message.Error);

        return Task.CompletedTask;
    }

    private static string? Validate(string authority, string clientId)
    {
        if (authority.Length == 0 && clientId.Length == 0)
            return null;

        if (authority.Length == 0 || clientId.Length == 0)
            return Localization.OpenIdProviderControlPanel.IncompleteProviderError;

        return ExternalIdProviderValidation.IsValidAuthority(authority)
            ? null
            : Localization.OpenIdProviderControlPanel.InvalidAuthorityError;
    }

    /// <summary>
    /// A typed secret beats the remove action, so changing one's mind needs no panel reset.
    /// </summary>
    private static ClientSecretUpdate ResolveSecretUpdate(OpenIdProviderControlPanelState state, bool removal)
    {
        if (removal)
            return ClientSecretUpdate.Clear;

        if (state.ClientSecret.Length > 0)
            return ClientSecretUpdate.Set(state.ClientSecret);

        return state.RemoveStoredClientSecret ? ClientSecretUpdate.Clear : ClientSecretUpdate.Keep;
    }

    private static void ApplySaved(OpenIdProviderControlPanelState state, string authority, string clientId,
        ClientSecretUpdate secretUpdate)
    {
        state.Authority = authority;
        state.ClientId = clientId;
        state.ClientSecret = string.Empty;
        state.RemoveStoredClientSecret = false;

        state.ClientSecretStored = secretUpdate.Kind switch
        {
            ClientSecretUpdateKind.Set => true,
            ClientSecretUpdateKind.Clear => false,
            _ => state.ClientSecretStored
        };
    }
}
