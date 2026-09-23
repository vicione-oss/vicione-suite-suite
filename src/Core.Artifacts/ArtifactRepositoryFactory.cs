using Core.Artifacts.Contracts;
using Sdk.Backend.Artifacts;

namespace Core.Artifacts;

/// <summary>
/// Creates artifact instances without dependency injection.
/// </summary>
public static class ArtifactRepositoryFactory
{
    public static IArtifact CreateArtifact(string name, string path, string repository, ArtifactKind artifactKind = ArtifactKind.File)
        => new Artifact() { Name = name, Path = path, Repository = repository, Kind = artifactKind };
}
