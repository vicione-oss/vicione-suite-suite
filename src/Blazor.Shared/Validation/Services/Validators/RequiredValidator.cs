using System.Diagnostics.CodeAnalysis;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Validation.Services.Validators;

internal sealed partial class RequiredValidator : IRequiredValidator
{
    public bool Validate([NotNullWhen(true)] string? password, string field, [MaybeNullWhen(true)] out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            errorMessage = string.Format(ValidationMessages.Culture, ValidationMessages.FieldIsRequired, field);

            return false;
        }

        errorMessage = default;

        return true;
    }
}
