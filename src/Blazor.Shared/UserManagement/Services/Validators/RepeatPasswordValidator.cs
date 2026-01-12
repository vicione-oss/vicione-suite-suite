using System.Diagnostics.CodeAnalysis;
using Blazor.Shared.Validation.Services.Validators;

namespace Blazor.Shared.UserManagement.Services.Validators;

internal sealed class RepeatPasswordValidator(IRequiredValidator requiredValidator) : IRepeatPasswordValidator
{
    public bool Validate(string repeatPassword, string repeatPasswordField, string password, [MaybeNullWhen(true)] out string errorMessage)
    {
        if (!requiredValidator.Validate(repeatPassword, repeatPasswordField, out errorMessage))
            return false;

        if (!string.Equals(repeatPassword, password, StringComparison.Ordinal))
        {
            errorMessage = Localization.RepeatPasswordValidator.PasswordsDoNotMatch;

            return false;
        }

        errorMessage = default;

        return true;
    }
}
