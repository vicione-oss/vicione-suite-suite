using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Validation.Services.Validators;

public sealed partial class PhoneNumberValidator : IPhoneNumberValidator
{
    public bool Validate([NotNullWhen(true)] string? phoneNumber, string field, [MaybeNullWhen(true)] out string errorMessage)
    {
        errorMessage = default;

        if (string.IsNullOrEmpty(phoneNumber))
            return true;

        if (phoneNumber.Length < 5)
        {
            errorMessage = string.Format(ValidationMessages.Culture, ValidationMessages.FieldMustNotHaveLessThanXCharacters, field, 5);

            return false;
        }

        var phoneNumberAttribute = new PhoneAttribute();

        if (!phoneNumberAttribute.IsValid(phoneNumber))
        {
            errorMessage = string.Format(ValidationMessages.Culture, ValidationMessages.FieldMustBePhoneNumber, field);

            return false;
        }

        return true;
    }
}
