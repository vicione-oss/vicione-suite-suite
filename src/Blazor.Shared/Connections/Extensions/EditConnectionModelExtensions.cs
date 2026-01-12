using Blazor.Shared.Connections.Contracts;
using Microsoft.AspNetCore.Components.Forms;
using Sdk.Client.Connections;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Connections.Extensions;

internal static class EditConnectionModelExtensions
{
    public static void ValidateTypedConnection(this EditConnectionModel model, IConnectionTypeUiRegistry connectionTypeUiRegistry)
    {
        if (model.TypedConnection is null)
            return;

        if (!connectionTypeUiRegistry.TryGetItemValidator(model.Type, out var itemValidator))
            throw new InvalidOperationException($"No item validator registered for connection type '{model.Type}'.");

        var validationErrors = itemValidator.Validate(model.TypedConnection)
            .ToDictionary(k => new FieldIdentifier(model.TypedConnection, k.Key), k => k.Value);

        if (validationErrors is not null && validationErrors.Count > 0)
        {
            // we can't display more errors actually so take the first we have
            var firstError = validationErrors.First();
            var errorMessage = firstError.Value.FirstOrDefault() ?? CommonPhrases.AnUnknownErrorOccurred;
            throw new InvalidOperationException(errorMessage);
        }
    }
}
