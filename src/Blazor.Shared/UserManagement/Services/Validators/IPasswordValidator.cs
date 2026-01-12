using System.Diagnostics.CodeAnalysis;

namespace Blazor.Shared.UserManagement.Services.Validators;

internal interface IPasswordValidator
{
    bool Validate(string password, string field, [MaybeNullWhen(true)] out string errorMessage);
}
