using System.Diagnostics.CodeAnalysis;
using Blazor.Shared.Validation.Services.Validators;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.UserManagement.Services.Validators;

internal sealed class UsernameValidator(IRequiredValidator requiredValidator) : IUsernameValidator
{
    public bool Validate(string username, [MaybeNullWhen(true)] out string errorMessage)
    {
        if (!requiredValidator.Validate(username, TechnicalTerms.Username, out errorMessage))
            return false;

        if (username.Length > 50)
        {
            errorMessage = string.Format(ValidationMessages.Culture, ValidationMessages.FieldMustNotHaveMoreThanXCharacters, TechnicalTerms.Username, 50);

            return false;
        }

        errorMessage = default;

        return true;
    }
}
