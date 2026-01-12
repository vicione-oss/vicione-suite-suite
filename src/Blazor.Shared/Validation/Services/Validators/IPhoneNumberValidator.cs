using System.Diagnostics.CodeAnalysis;

namespace Blazor.Shared.Validation.Services.Validators;

internal interface IPhoneNumberValidator
{
    bool Validate([NotNullWhen(true)] string? phoneNumber, string field, [MaybeNullWhen(true)] out string errorMessage);
}

