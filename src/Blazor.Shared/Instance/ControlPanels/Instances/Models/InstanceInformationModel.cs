using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sdk.Instance;

namespace Blazor.Shared.Instance.ControlPanels.Instances.Models;

public sealed class InstanceInformationModel(IInstanceInformation info) : IInstanceInformation
{
    public Guid Id { get; } = info.Id;

    public string? Name { get; set; } = info.Name;

    public string FormattedName { get; set; } = info.FormattedName;

    public string? Description { get; set; } = info.Description;

    public List<string> InstalledModules { get; } = [.. info.InstalledModules];

    public DateTimeOffset? FirstTimeRegistered { get; } = info.FirstTimeRegistered;

    public DateTimeOffset? LastRegistered { get; } = info.LastRegistered;

    public InstanceType Type { get; } = info.Type;

    public bool IsSynchronizing { get; set; }

    public bool HasFailed { get; set; }

    public HealthStatus? HealthStatus { get; set; }

    public DateTimeOffset? LastSent { get; set; }

    public string SerialNumber { get; } = info.SerialNumber;

    public string SystemType { get; set; } = info.SystemType;

    public string Version { get; } = info.Version;

    public string SdkVersion { get; } = info.SdkVersion;

    public string? BranchName { get; } = info.BranchName;

    public bool InRecoveryMode { get; set; }

    IReadOnlyCollection<string> IInstanceInformation.InstalledModules => throw new NotImplementedException();
}
