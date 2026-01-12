using Blazor.Shared.Localization;
using Sdk.Client.Connections;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Connections.Validators;

public class AzureIotConnectionValidator : ItemValidatorBase<AzureIotHubConnection>
{
    protected override void ValidateInternal(AzureIotHubConnection item)
    {
        if (string.IsNullOrWhiteSpace(item.Hostname))
        {
            AddError(nameof(AzureIotHubConnection.Hostname), ValidationTerms.ProvideHttpAddress);
        }
    }
}
