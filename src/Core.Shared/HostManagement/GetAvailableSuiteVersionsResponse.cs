using Sdk.Messaging;

namespace Core.Shared.HostManagement;

public record GetAvailableSuiteVersionsResponse(List<SuiteVersionPackage> Versions) : IResponse
{
    public ErrorInfo? RequestError { get; set; }
}

public class SuiteVersionPackage
{
    public required string Architecture { get; set; }

    public required string HostManagementVersion { get; set; }

    public required string PackageName { get; set; }

    public required string SignatureName { get; set; }

    public required string Version { get; set; }

    public required bool Installed { get; set; }
}
