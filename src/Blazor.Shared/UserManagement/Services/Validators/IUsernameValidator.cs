using System.Diagnostics.CodeAnalysis;

namespace Blazor.Shared.UserManagement.Services.Validators;

internal interface IUsernameValidator
{
    bool Validate(string username, [MaybeNullWhen(true)] out string errorMessage);
}
