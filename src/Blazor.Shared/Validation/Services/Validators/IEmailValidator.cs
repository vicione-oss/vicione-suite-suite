using System.Diagnostics.CodeAnalysis;

namespace Blazor.Shared.Validation.Services.Validators;

internal interface IEmailValidator
{
    bool Validate([NotNullWhen(true)] string? email, string field, [MaybeNullWhen(true)] out string errorMessage);
}
