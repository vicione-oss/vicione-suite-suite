namespace Core.OS.UserManagement.Security;

public static class LockoutDefaults
{
    public const int MaxFailedAccessAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);
}
