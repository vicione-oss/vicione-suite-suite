using System.ComponentModel.DataAnnotations;
using Core.OS.Instance.HealthCheck;
using Core.Shared.Instance.Contracts;
using Microsoft.Extensions.Options;
using Sdk.Instance;

namespace Core.OS.Instance;

public sealed class InstanceOptions
{
    public const string ConfigSection = "Instance";

    [Required]
    public required string HomeDirectory { get; set; }

    [Required]
    public required string CacheDirectory { get; set; }

    [Required]
    public required string BackupDirectory { get; set; }

    [Required]
    public required InstanceType Type { get; set; }

    public Guid? IdPreload { get; set; }

    public string? SerialNumber { get; set; }

    [StringLength(100)]
    public string? NamePreload { get; set; }

    [StringLength(255)]
    public string? DescriptionPreload { get; set; }

    public string? FormattedName { get; set; }

    public bool UseExternalSecurity { get; set; }

    public bool UseHeaderForwarding { get; set; }

    public string ServiceName { get; set; } = "vicione-suite.service";

    public LoginDesign LoginDesign { get; set; } = LoginDesign.Default;

    [ValidateObjectMembers]
    public InstanceHealthCheckOptions? HealthChecks { get; set; }

    public InstanceRecoveryOptions? Recovery { get; set; }
}
