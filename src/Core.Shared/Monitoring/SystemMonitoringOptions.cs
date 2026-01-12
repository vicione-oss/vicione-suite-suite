namespace Core.Shared.Monitoring;

public sealed class SystemMonitoringOptions
{
    public const string ConfigSection = "SystemMonitoring";

    public bool Enabled { get; set; } = true;

    public string? DefaultNetworkInterface { get; set; } = "eth0";

    public string SuiteJournalFilter { get; set; } = "SYSLOG_IDENTIFIER=ViciOne.Suite.Core.OS+_SYSTEMD_UNIT=vicione-suite.service+_SYSTEMD_UNIT=hostmanagement.service";

    public CollectorOptions Collectors { get; set; } = new();
}
