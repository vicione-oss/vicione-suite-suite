using Core.Module.Options;

namespace Core.Module;

/// <summary>
/// Interface to be implemented by services capable of providing <see cref="ArtifactRepositoryOptions"/>. 
/// This is used to decouple the artifact repository from the actual source of the artifact repository configuration.
/// </summary>
public interface IArtifactRepositoryOptionsProvider
{
    ArtifactRepositoryOptions GetOptions();
}
