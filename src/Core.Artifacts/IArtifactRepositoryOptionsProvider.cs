namespace Core.Artifacts;

/// <summary>
/// Decouples the artifact repository from the source of its configuration.
/// </summary>
public interface IArtifactRepositoryOptionsProvider
{
    ArtifactRepositoryOptions GetOptions();
}
