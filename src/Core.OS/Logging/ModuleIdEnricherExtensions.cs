using Serilog.Configuration;

namespace Serilog.Sinks.Journal;

public static class ModuleIdEnricherExtensions
{
    public static LoggerConfiguration WithModuleId(this LoggerEnrichmentConfiguration enrichmentConfiguration)
        => enrichmentConfiguration.With<ModuleIdEnricher>();
}
