namespace Core.Shared.Security;

public enum ExternalLoginError
{
    None = 0,
    LoginFailed,
    NoLocalUser,
    UnknownExternalUser
}
