using Blazor.Shared.Connections.Contracts;
using Blazor.Shared.Connections.Extensions;
using Core.Shared.Connections.Contracts;
using Sdk.Client.Connections;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Connections.Contracts;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Connections.Services;

internal sealed class ConnectionControlPanelSaveHandler(ISuiteConnectionService suiteConnectionService, IConnectionTypeUiRegistry connectionTypeUiRegistry)
    : IControlPanelSaveHandler<ConnectionControlPanelState>
{
    public async Task<ISaveResult> Save(ConnectionControlPanelState state, CancellationToken cancellationToken)
    {
        if (state.EditConnectionModel is null)
            throw new InvalidOperationException("No edit model provided");

        if (string.IsNullOrWhiteSpace(state.EditConnectionModel.Connection.Name))
            return new SaveErrorResult(string.Format(ValidationMessages.Culture, ValidationMessages.FieldMustNotBeEmpty, nameof(Connection.Name)));

        if (state.EditConnectionModel.Connection.Name.Length > Constraints.ConnectionNameMaximumLength)
            return new SaveErrorResult(string.Format(ValidationMessages.Culture, ValidationMessages.FieldMustNotHaveMoreThanXCharacters, nameof(Connection.Name), Constraints.ConnectionNameMaximumLength));

        if (suiteConnectionService.IsNameAlreadyUsed(state.EditConnectionModel.Connection))
            return new SaveErrorResult(string.Format(ValidationMessages.Culture, ValidationMessages.FieldHasToBeUnique, nameof(Connection.Name)));

        state.EditConnectionModel.ValidateTypedConnection(connectionTypeUiRegistry);

        var result = await suiteConnectionService.UpsertConnectionAndTags(state.EditConnectionModel, state.AvailableTags, state.EditModelTagTexts, cancellationToken);

        if (result is SuiteConnectionServiceSuccessResult)
            return new SaveSuccessResult();

        if (result is SuiteConnectionServiceErrorResult errorResult)
            return new SaveErrorResult(errorResult.ErrorMessage);

        throw new NotSupportedException("Result type unknown");
    }
}
