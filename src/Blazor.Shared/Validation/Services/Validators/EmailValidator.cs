using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Validation.Services.Validators;

internal sealed partial class EmailValidator : IEmailValidator
{
    public bool Validate([NotNullWhen(true)] string? email, string field, [MaybeNullWhen(true)] out string errorMessage)
    {
        errorMessage = default;

        if (string.IsNullOrEmpty(email))
            return true;

        if (!IsEmail().Match(email).Success)
        {
            errorMessage = string.Format(ValidationMessages.Culture, ValidationMessages.FieldMustBeEmailAddress, field);

            return false;
        }

        return true;
    }

    // https://regex101.com/r/Of0NAO/1
    [GeneratedRegex("^((?!\\.)[\\w\\-_.]*[^.])@(\\w+(-\\w+)*)(\\.\\w+(\\.\\w+)?[^.\\W\\d])$")]
    private static partial Regex IsEmail();
}
