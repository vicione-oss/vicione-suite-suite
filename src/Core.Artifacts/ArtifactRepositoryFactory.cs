using System.IO.Abstractions;
using Core.Artifacts.Contracts;
using Core.Artifacts.JFrog;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Artifacts;

namespace Core.Artifacts;

/// <summary>
/// Provides factory methods for creating artifact-related instances without relying on dependency injection.
/// </summary>
public static class ArtifactRepositoryFactory
{
    /// <summary>
    /// Creates a new <see cref="IArtifactRepository"/> backed by JFrog Artifactory.
    /// </summary>
    /// <param name="fileSystem">The file system abstraction used for file operations.</param>
    /// <param name="httpClientFactory">The factory used to create <see cref="HttpClient"/> instances.</param>
    /// <param name="optionsProvider">The provider supplying the artifact repository connection options.</param>
    /// <param name="loggerFactory">The factory used to create a logger for the repository.</param>
    /// <returns>A new <see cref="IArtifactRepository"/> instance.</returns>
    public static IArtifactRepository Create(IFileSystem fileSystem,
        IHttpClientFactory httpClientFactory,
        IArtifactRepositoryOptionsProvider optionsProvider,
        ILoggerFactory loggerFactory)
        => new JFrogArtifactRepository(fileSystem, httpClientFactory, optionsProvider, loggerFactory.CreateLogger<JFrogArtifactRepository>());

    /// <summary>
    /// Creates a new <see cref="IArtifact"/> with the specified properties.
    /// </summary>
    /// <param name="name">The file name of the artifact.</param>
    /// <param name="path">The repository path of the artifact.</param>
    /// <param name="repository">The name of the repository the artifact belongs to.</param>
    /// <param name="artifactKind">The kind of the artifact. Defaults to <see cref="ArtifactKind.File"/>.</param>
    /// <returns>A new <see cref="IArtifact"/> instance.</returns>
    public static IArtifact CreateArtifact(string name, string path, string repository, ArtifactKind artifactKind = ArtifactKind.File)
        => new Artifact() { Name = name, Path = path, Repository = repository, Kind = artifactKind };
}
