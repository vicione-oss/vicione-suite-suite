using System.Diagnostics.CodeAnalysis;

namespace Blazor.Shared.Network.Services.Validators;

internal interface IIpAddressValidator
{
    bool Validate(string value, string field, [MaybeNullWhen(true)] out string errorMessage);
}
