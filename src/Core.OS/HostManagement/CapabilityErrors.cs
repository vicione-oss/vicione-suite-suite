namespace Core.OS.HostManagement;

internal static class CapabilityErrors
{
    /// <param name="topic">One of <see cref="global::HostManagement.Shared.Communication.Topics"/>.</param>
    public static string Disabled(string topic) => $"The capability '{topic}' is disabled in HostManagement.";
}
