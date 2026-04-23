using Core.Artifacts;

namespace Core.Tests.Tools;

public static class SystemTestSettings
{
    public static ArtifactRepositoryOptions GetArtifactRepositoryOptions() => new()
    {
        Sources = [
            new ArtifactRepositorySourceOption {
                Endpoint = "https://ifm.jfrog.io/artifactory/vicione-suite",
                UserName = "vicione-suite-readonly",
                Password = "cmVmdGtuOjAxOjE3NzYxNDczMDk6RlpsVEExSWV6QjR0ckVFeG56UmVLZ2dhczRN",
            },
            new ArtifactRepositorySourceOption {
                Endpoint = "https://ifm.jfrog.io/artifactory/vicione-suite-staging",
                UserName = "vicione-suite-readonly",
                Password = "cmVmdGtuOjAxOjE3NzYxNDczMDk6RlpsVEExSWV6QjR0ckVFeG56UmVLZ2dhczRN",
            },
            new ArtifactRepositorySourceOption {
                Endpoint = "https://ifm.jfrog.io/artifactory/vicione-suite-dev",
                UserName = "vicione-suite-readonly",
                Password = "cmVmdGtuOjAxOjE3NzYxNDczMDk6RlpsVEExSWV6QjR0ckVFeG56UmVLZ2dhczRN",
            }]
    };
}
