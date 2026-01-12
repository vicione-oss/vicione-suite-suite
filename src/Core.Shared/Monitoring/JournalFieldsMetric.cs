namespace Core.Shared.Monitoring;

public static class JournalFieldsMetric
{
    public const string Suite = "suite";
    public const string Count = "count";
    public const string SyslogIdentifier = "SYSLOG_IDENTIFIER";

    public static string SuiteAssemblyName { get; set; } = string.Empty;
}
