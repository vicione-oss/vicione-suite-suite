namespace Core.Shared.Passkeys;

public enum PasskeyError
{
    UserNotFound = 0,
    PasskeyNotFound = 1,
    NameInvalid = 100,
    NameAlreadyInUse = 101,
    RenameFailed = 200,
    UnknownError = 999
}
