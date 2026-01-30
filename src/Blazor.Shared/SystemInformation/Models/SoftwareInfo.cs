using Sdk.Instance;

namespace Blazor.Shared.SystemInformation.Models;

public readonly struct SoftwareInfo
{
    public required string SuiteVersion { get; init; }
    public required string SdkVersion { get; init; }
    public required InstanceType InstanceType { get; init; }
    public required string SerialNumber { get; init; }
    public required Dictionary<string, string> ModuleVersions { get; init; }

    public override string ToString()
    {
        var modulesList = ModuleVersions.Count != 0
            ? string.Join(Environment.NewLine, ModuleVersions.Select(kv => $" - {kv.Key}: v{kv.Value}"))
            : " - no modules loaded";
        return $"""
                Suite: v{SuiteVersion}
                Sdk: v{SdkVersion}
                Type: {InstanceType}
                Sn.: {SerialNumber}
                Modules:
                {modulesList}
                """;
    }
}
