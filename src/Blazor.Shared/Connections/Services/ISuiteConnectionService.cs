using Blazor.Shared.Connections.Contracts;
using Sdk.Client.Services;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;

namespace Blazor.Shared.Connections.Services;

public interface ISuiteConnectionService : IConnectionService
{
    IReadOnlySet<Tag> CachedTags { get; }

    event Func<ConnectionChanged, Task>? ConnectionChanged;

    event Func<TagsChanged, Task>? TagsChanged;

    Task Initialize(CancellationToken cancellationToken = default);

    Task<Connection?> GetConnection(Guid connectionId, CancellationToken cancellationToken = default);
    Task<List<Connection>> GetConnections(List<string>? filterTypes, CancellationToken cancellationToken = default);
    Task<ISuiteConnectionServiceResult> UpsertConnection(Connection connection, CancellationToken cancellationToken = default);
    Task<ISuiteConnectionServiceResult> DeleteConnection(Connection connection, CancellationToken cancellationToken = default);

    Task<Tag?> GetTag(Guid tagId, CancellationToken cancellationToken = default);
    Task<List<Tag>> GetTags(CancellationToken cancellationToken = default);
    Task<ISuiteConnectionServiceResult> UpsertTag(Tag tag, CancellationToken cancellationToken = default);
    Task<ISuiteConnectionServiceResult> DeleteTag(Tag tag, CancellationToken cancellationToken = default);
}
