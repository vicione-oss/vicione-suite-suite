using Blazor.Shared.Localization;
using Sdk.Client.Connections;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Connections.Validators;

public class HttpConnectionValidator : ItemValidatorBase<HttpConnection>
{
    protected override void ValidateInternal(HttpConnection item)
    {
        if (string.IsNullOrWhiteSpace(item.BaseAddress))
        {
            AddError(nameof(HttpConnection.BaseAddress), ValidationTerms.ProvideHttpAddress);
        }
        if (!Uri.IsWellFormedUriString(item.BaseAddress, UriKind.Absolute))
        {
            AddError(nameof(HttpConnection.BaseAddress), ValidationTerms.ProvideHttpAddress);
        }
    }
}
