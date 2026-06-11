using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;
using PasskeyConstants = Core.Shared.Passkeys.Constants;

namespace Blazor.Shared.Profile.ControlPanels.Passkeys;

public class AddPasskeyControlPanelSaveHandler(
    AntiforgeryStateProvider antiforgeryStateProvider,
    IOptions<AntiforgeryOptions> antiforgeryOptions,
    IJSRuntime jsRuntime,
    ILogger<AddPasskeyControlPanelSaveHandler> logger) : IControlPanelSaveHandler<AddPasskeysControlPanelState>
{
    public async Task<ISaveResult> Save(AddPasskeysControlPanelState state, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state.Name))
            return new SaveErrorResult(string.Format(ValidationMessages.Culture,
                ValidationMessages.FieldIsRequired,
                nameof(state.Name)));

        if (state.Name.Length > PasskeyConstants.MaxPasskeyNameLength)
            return new SaveErrorResult(string.Format(ValidationMessages.Culture,
                ValidationMessages.FieldMustNotHaveMoreThanXCharacters,
                nameof(state.Name),
                PasskeyConstants.MaxPasskeyNameLength));

        if (NameIsAlreadyTaken(state))
            return new SaveErrorResult(
                string.Format(ValidationMessages.Culture, ValidationMessages.FieldHasToBeUnique, nameof(state.Name)));

        try
        {
            await CreateAndSendPasskeyRequest(state, cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error saving passkey");
            return new SaveErrorResult(Localization.ValidationMessages.ErrorSavingPasskey);
        }

        return new SaveSuccessResult();
    }

    private static bool NameIsAlreadyTaken(AddPasskeysControlPanelState state)
        => state.ExistingUserPasskeys.Select(info => info.Name)
            .Contains(state.Name, StringComparer.OrdinalIgnoreCase);

    private async Task CreateAndSendPasskeyRequest(AddPasskeysControlPanelState state,
        CancellationToken cancellationToken)
    {
        var headerName = antiforgeryOptions.Value.HeaderName!;
        var token = antiforgeryStateProvider.GetAntiforgeryToken()?.Value;

        await jsRuntime.InvokeAsync<string>(
            "SuitePasskeys.ObtainAndCreateCredentials",
            cancellationToken,
            state.Name!,
            headerName,
            token);
    }
}
