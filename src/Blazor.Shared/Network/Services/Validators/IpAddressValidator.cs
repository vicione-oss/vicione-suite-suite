using System.Diagnostics.CodeAnalysis;
using System.Net;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Network.Services.Validators;

internal sealed partial class IpAddressValidator : IIpAddressValidator
{
    public bool Validate(string value, string field, [MaybeNullWhen(true)] out string errorMessage)
    {
        if (!IPAddress.TryParse(value, out _))
        {
            errorMessage = string.Format(ValidationMessages.Culture, Localization.IpAddressValidator.MustBeAValidIpAddress, field);

            return false;
        }

        errorMessage = default;

        return true;
    }
}
