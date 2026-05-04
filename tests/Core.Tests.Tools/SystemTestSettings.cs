using Core.Artifacts;

namespace Core.Tests.Tools;

public static class SystemTestSettings
{
    public static ArtifactRepositoryOptions GetArtifactRepositoryOptions() => new()
    {
        Sources = [
            new ArtifactRepositorySourceOption {
                Endpoint = "https://ifm.jfrog.io/artifactory/vicione-suite",
                UserName = "<user>",
                Password = "<password>",
            }]
    };
}
