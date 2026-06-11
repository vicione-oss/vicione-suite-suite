namespace Core.Shared.Security;

public static class ExternalLoginErrorConstants
{
    public const string ExternalLoginErrorKey = "ExternalError";
}

public enum ExternalLoginError
{
    None = 0,
    LoginFailed,
    NoLocalUser,
    UnknownExternalUser,
    NewUserCreationFailed,
    ExternalAccountAlreadyAssociated
}
