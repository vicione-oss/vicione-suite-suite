namespace Core.OS.Instance.Contracts;

/// <summary>
/// We have to invalidate users security timestamps after updating the system
/// via swu or backup restore. This way we ensure that the user needs to login
/// again because the user-db might gets modified on these tasks.
/// </summary>
internal static class InstanceStartupState
{
    /// <summary>
    /// We can't directly invalidate the stamps on consuming the restore command
    /// because the database will be restored on next restart. This happens
    /// before service provider is built but we need to ensure the users have
    /// to login again.
    /// </summary>
    public static bool InvalidateLoginsAfterMigration { get; set; }
}
