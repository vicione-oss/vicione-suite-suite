using System.IO.Abstractions;
using System.Reflection;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;

namespace Core.OS.Diagnostics;

/// <summary>
/// Provides the OpenTelemetry resource identity shared between the
/// traces/metrics pipeline and the Serilog OpenTelemetry log sink, so logs and
/// traces report under the same service and instance in the backend.
/// </summary>
internal static class SuiteOtelResource
{
    public const string ServiceNamespace = "vicione";

    public static string GetServiceName(OtelExporterOptions exporterOptions)
        => exporterOptions.ServiceName ?? CoreActivitySource.SourceName;

    public static string? GetServiceVersion()
        => Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

    public static IDictionary<string, object> GetResourceAttributes(OtelExporterOptions exporterOptions,
        IFileSystem fileSystem,
        InstanceOptions instanceOptions)
    {
        var attributes = InitializeServiceAttributes(exporterOptions);
        AddServiceVersionAttribute(attributes);
        AddInstanceTypeAttribute(instanceOptions, attributes);
        AddInstanceIdAttribute(fileSystem, instanceOptions, attributes);

        return attributes;
    }

    private static Dictionary<string, object> InitializeServiceAttributes(OtelExporterOptions exporterOptions)
        => new()
        {
            ["service.name"] = GetServiceName(exporterOptions),
            ["service.namespace"] = ServiceNamespace,
            ["process.pid"] = Environment.ProcessId,
        };

    private static void AddInstanceTypeAttribute(InstanceOptions instanceOptions, Dictionary<string, object> attributes)
        => attributes["service.instance.type"] = instanceOptions.Type.ToString();

    private static void AddInstanceIdAttribute(IFileSystem fileSystem,
        InstanceOptions instanceOptions,
        Dictionary<string, object> attributes)
    {
        if (fileSystem.ReadLocalInstanceId(instanceOptions) is { } instanceId)
            attributes["service.instance.id"] = instanceId;
    }

    private static void AddServiceVersionAttribute(Dictionary<string, object> attributes)
    {
        if (GetServiceVersion() is { Length: > 0 } serviceVersion)
            attributes["service.version"] = serviceVersion;
    }
}
