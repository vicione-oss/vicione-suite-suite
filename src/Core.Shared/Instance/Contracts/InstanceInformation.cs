using System.Globalization;
using Sdk.Instance;

namespace Core.Shared.Instance.Contracts;

public sealed class InstanceInformation : IInstanceInformation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string? Name { get; set; }

    /// <summary>
    /// Text inside the braces is highlighted; the braces are replaced by markup such as a span.
    /// </summary>
    public string FormattedName { get; set; } = "{ViciOne} Suite";
    public string? Description { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string SystemType { get; set; } = string.Empty;
    public List<string> InstalledModules { get; set; } = [];
    IReadOnlyCollection<string> IInstanceInformation.InstalledModules => InstalledModules;
    public DateTimeOffset? FirstTimeRegistered { get; set; }
    public DateTimeOffset? LastRegistered { get; set; }
    public InstanceType Type { get; set; }
    public string Version { get; set; } = string.Empty;
    public string? BranchName { get; set; }
    public string SdkVersion { get; set; } = string.Empty;

    public bool InRecoveryMode { get; set; }

    public override string ToString()
    {
        return string.Format(CultureInfo.InvariantCulture, "Id:{0} Type:{1} Name:{2} Modules:{3}",
            Id, Type, Name, string.Join(", ", InstalledModules));
    }
}
