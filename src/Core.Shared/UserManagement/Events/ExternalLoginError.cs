namespace Core.Shared.UserManagement.Events;

public enum ExternalLoginError
{
    UserNotFound = 0,
    LastCredential = 1,
    Failed = 2
}
