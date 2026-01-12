using System.Diagnostics;
using Sdk.Backend.ArtifactApi;

namespace Core.Module.Contracts;

[DebuggerDisplay("{Id} Path:{Path} ContentType:{ContentType}")]
public class ModuleArtifactInfo : IArtifactItem
{
    /// <summary>
    /// A unique identifier for the artifact within its repository context
    /// (e.g., Nexus Asset ID, Artifactory Path, etc.). Useful for correlation.
    /// </summary>
    public string Id => $"{Repo}#{Path}#{Name}";

    /// <summary>
    /// The full path or identifier of the artifact within the repository's structure.
    /// Example: "1.2.0-linux-x64.zip"
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The relative path or identifier of the artifact within the repository's structure.
    /// Example: "/modules/ViciOne.Module.Name/"
    /// </summary>
    public required string Path { get; set; }


    public required string Repo { get; set; }

    /// <summary>
    /// Size of the artifact in bytes.
    /// </summary>
    public long? Size { get; set; }

    /// <summary>
    /// Last modification timestamp of the artifact. Use DateTimeOffset for timezone awareness.
    /// </summary>
    public DateTimeOffset? Modified { get; set; }

    /// <summary>
    /// Checksum information for verifying integrity. Optional.
    /// </summary>
    public IArtifactChecksum? Checksum { get; set; }

    /// <summary>
    /// Mime content type if known like "application/zip", "application/json"
    /// </summary>
    public string? ContentType { get; set; }
}
