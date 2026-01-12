using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Onboarding.Services.Validators;

internal sealed partial class HostnameValidator : IHostnameValidator
{
    /// https://regex101.com/r/ml2h53/4
    [GeneratedRegex(@"[^A-Za-z0-9-]", RegexOptions.CultureInvariant)]

    private partial Regex Matcher();

    public bool Validate(string hostname, string field, [MaybeNullWhen(true)] out string errorMessage)
    {
        if (hostname.StartsWith('-'))
        {
            errorMessage = string.Format(ValidationMessages.Culture, Localization.HostnameValidator.MustNotStartWithHypenCharacter, field);

            return false;
        }

        if (hostname.EndsWith('-'))
        {
            errorMessage = string.Format(ValidationMessages.Culture, Localization.HostnameValidator.MustNotEndWithHypenCharacter, field);

            return false;
        }

        var containsInvalidCharacters = Matcher().IsMatch(hostname);
        if (containsInvalidCharacters)
        {
            errorMessage = string.Format(ValidationMessages.Culture, Localization.HostnameValidator.MustContainOnlyAlphanumericCharactersAndHyphens, field);

            return false;
        }

        errorMessage = default;

        return true;
    }
}
