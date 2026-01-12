using System.Diagnostics.CodeAnalysis;

namespace Blazor.Shared.UserManagement.Services.Validators;

internal interface IRepeatPasswordValidator
{
    bool Validate(string repeatPassword, string repeatPasswordField, string password, [MaybeNullWhen(true)] out string errorMessage);
}
