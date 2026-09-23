using Blazor.Shared.Connections.Contracts;
using Blazor.Shared.Connections.Services;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Connections.Extensions;

internal static class ISuiteConnectionServiceExtensions
{
    public static async Task<ISuiteConnectionServiceResult> UpsertConnectionAndTags(this ISuiteConnectionService service, EditConnectionModel model, List<Tag> availableTags, IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        var connectionCopy = new Connection()
        {
            Json = model.Connection.Json,
            Description = model.Connection.Description,
            Id = model.Connection.Id,
            Name = model.Connection.Name,
            Type = model.Connection.Type,
            Managed = model.Connection.Managed,
            Tags = BuildConnectionTags(availableTags, tags),
            Metadata = new Dictionary<string, string?>(model.Connection.Metadata)
        };

        // Will trigger ConnectionChanged event and on consume the changes will get applied to the model
        return await service.UpsertConnection(connectionCopy, cancellationToken);
    }

    private static HashSet<Tag> BuildConnectionTags(List<Tag> availableTags, IEnumerable<string> tags)
    {
        var result = new HashSet<Tag>();

        foreach (var tag in tags)
        {
            var selectedTag = availableTags.FirstOrDefault(t => t.Text == tag);
            if (selectedTag is not null)
            {
                result.Add(selectedTag);
                continue;
            }

            var newTag = new Tag(tag);
            result.Add(newTag);
        }

        return result;
    }

    /// <summary>
    /// Returns true when another connection already uses the same name.
    /// </summary>
    internal static bool IsNameAlreadyUsed(this ISuiteConnectionService service, Connection connection)
        => service.Connections.Any(c => connection.Id != c.Id && string.Equals(c.Name, connection.Name, StringComparison.Ordinal));
}
