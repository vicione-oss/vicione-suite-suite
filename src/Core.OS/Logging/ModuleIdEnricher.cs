using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.Journal;

public sealed class ModuleIdEnricher : ILogEventEnricher
{
    private const string ModuleSuffixInternal = ".Internal";
    private const string ModuleSuffixPublic = ".Public";
    private const string ModuleSuffixBackend = ".Backend";
    private const string ModuleSuffixClient = ".Client";

    private const string ModuleIdPropertyName = "ModuleId";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (TryGetModuleIdFromStackTrace(8, [ModuleSuffixInternal, ModuleSuffixPublic, ModuleSuffixBackend, ModuleSuffixClient], out var moduleId))
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(ModuleIdPropertyName, moduleId));
    }

    internal static bool TryGetModuleIdFromStackTrace(int offset, ReadOnlySpan<string> moduleIdSuffixes, [NotNullWhen(true)] out string? moduleId)
    {
        var frames = new StackTrace(offset).GetFrames();
        moduleId = null;

        foreach (var frame in frames)
        {
            var assemblyName = frame.GetMethod()?.DeclaringType?.Assembly.GetName().Name;

            if (TryGetModuleId(moduleIdSuffixes, assemblyName, out moduleId))
                return true;
        }

        return false;
    }

    internal static bool TryGetModuleId(ReadOnlySpan<string> moduleIdSuffixes, string? assemblyName, [NotNullWhen(true)] out string? moduleId)
    {
        moduleId = null;

        if (string.IsNullOrEmpty(assemblyName))
            return false;

        var nameSpan = assemblyName.AsSpan();

        foreach (var suffix in moduleIdSuffixes)
        {
            var suffixSpan = suffix.AsSpan();

            if (nameSpan.EndsWith(suffixSpan, StringComparison.Ordinal))
            {
                moduleId = nameSpan[..^suffixSpan.Length].ToString();
                return true;
            }
        }

        return false;
    }
}
