using Sdk.Backend.Artifacts;
using Sdk.Messaging;

namespace Core.Module.Contracts;

internal class ArtifactQueryError : IArtifactQueryError
{
    public required string Source { get; set; }

    public required ErrorInfo Error { get; set; }
}
