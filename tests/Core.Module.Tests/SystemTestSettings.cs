using Core.Artifacts;

namespace Core.Module.Tests;

internal static class SystemTestSettings
{
    /// <summary>
    /// Add source settings here you want to use for the system tests
    /// </summary>
    public static ArtifactRepositoryOptions ArtifactApiOptions = new()
    {
        Sources = [
            new ArtifactRepositorySourceOption {
                Endpoint = "https://ifm.jfrog.io/artifactory/vicione-suite",
                UserName = "<user>",
                Password = "<password>",
            }]
    };
}
