using System.Diagnostics.CodeAnalysis;

namespace Blazor.Shared.Onboarding.Services.Validators;

internal interface IHostnameValidator
{
    bool Validate(string hostname, string field, [MaybeNullWhen(true)] out string errorMessage);
}
