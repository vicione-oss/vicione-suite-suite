using System.Diagnostics.CodeAnalysis;

namespace Blazor.Shared.Validation.Services.Validators;

internal interface IRequiredValidator
{
    bool Validate([NotNullWhen(true)] string? value, string field, [MaybeNullWhen(true)] out string errorMessage);
}
