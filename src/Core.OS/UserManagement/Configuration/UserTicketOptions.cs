namespace Core.OS.UserManagement.Configuration;

public class UserTicketOptions
{
    /// <summary>
    /// Hour interval in which expired sessions will be deleted from the system.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromDays(1);
}
