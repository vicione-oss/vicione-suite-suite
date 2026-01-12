using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Blazor.Shared.Validation.Services.Validators;
using ViciOne.Ui.Localization.Resources;
using static Core.Shared.Constants;

namespace Blazor.Shared.UserManagement.Services.Validators;

internal sealed partial class PasswordValidator(IRequiredValidator requiredValidator) : IPasswordValidator
{
    private sealed class CaptureGroupNames
    {
        public const string UppercaseLetters = "uppercase_letters";
        public const string LowercaseLetters = "lowercase_letters";
        public const string Digits = "digits";
        public const string SpecialCharacters = "special_characters";
    };

    // https://regex101.com/r/dHwiSq/2
    [GeneratedRegex(@"(?'" + CaptureGroupNames.UppercaseLetters + "'[A-Z]+)|(?'" + CaptureGroupNames.LowercaseLetters + "'[a-z]+)|(?'" + CaptureGroupNames.Digits + "'[0-9]+)|(?'" + CaptureGroupNames.SpecialCharacters + "'[\\W]+)",
        RegexOptions.CultureInvariant)]

    private partial Regex Matcher();

    public bool Validate(string password, string field, [MaybeNullWhen(true)] out string errorMessage)
    {
        if (!requiredValidator.Validate(password, field, out errorMessage))
            return false;

        if (password.Length < MinimumPasswordLength)
        {
            errorMessage = string.Format(ValidationMessages.Culture, ValidationMessages.FieldMustNotHaveLessThanXCharacters, field, MinimumPasswordLength);

            return false;
        }

        var matches = Matcher().Matches(password);

        var groupNames = matches.SelectMany<Match, Group>(match => match.Groups)
            .Where(group => group.Name != "0" && group.Captures.Count > 0)
            .Select(g => g.Name)
            .Distinct()
            .ToList();

        if (!groupNames.Contains(CaptureGroupNames.UppercaseLetters))
        {
            errorMessage = string.Format(ValidationMessages.Culture, ValidationMessages.FieldMustContainOneOrMoreUppercaseLetters, field);

            return false;
        }

        if (!groupNames.Contains(CaptureGroupNames.LowercaseLetters))
        {
            errorMessage = string.Format(ValidationMessages.Culture, ValidationMessages.FieldMustContainOneOrMoreLowercaseLetters, field);

            return false;
        }

        if (!groupNames.Contains(CaptureGroupNames.Digits))
        {
            errorMessage = string.Format(ValidationMessages.Culture, ValidationMessages.FieldMustContainOneOrMoreDigits, field);

            return false;
        }

        if (!groupNames.Contains(CaptureGroupNames.SpecialCharacters))
        {
            errorMessage = string.Format(ValidationMessages.Culture, ValidationMessages.FieldMustContainOneOrMoreSpecialCharacters, field);

            return false;
        }

        errorMessage = default;

        return true;
    }
}
