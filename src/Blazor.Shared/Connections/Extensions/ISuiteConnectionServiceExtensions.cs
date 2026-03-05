using Blazor.Shared.Connections.Contracts;
using Blazor.Shared.Connections.Services;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Connections.Extensions;

internal static class ISuiteConnectionServiceExtensions
{
    public static async Task<ISuiteConnectionServiceResult> UpsertConnectionAndTags(this ISuiteConnectionService service, EditConnectionModel model, List<Tag> availableTags, IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        UpdateConnectionTags(model.Connection, availableTags, tags);

        return await service.UpsertConnection(model.Connection, cancellationToken);
    }

    private static void UpdateConnectionTags(Connection connection, List<Tag> availableTags, IEnumerable<string> tags)
    {
        var previousTags = connection.Tags.ToList();
        foreach (var tag in tags)
        {
            var existingTag = previousTags.FirstOrDefault(t => t.Text == tag);
            if (existingTag is not null)
            {
                previousTags.Remove(existingTag);
                continue;
            }

            var selectedTag = availableTags.FirstOrDefault(t => t.Text == tag);
            if (selectedTag is not null)
            {
                connection.Tags.Add(selectedTag);
                continue;
            }

            var newTag = new Tag(tag);
            connection.Tags.Add(newTag);
        }

        foreach (var deletedTag in previousTags)
        {
            connection.Tags.Remove(deletedTag);
        }
    }

    /// <summary>
    /// Returns true if a connection with another id has already the same name
    /// </summary>
    /// <param name="service"></param>
    /// <param name="connection"></param>
    /// <returns></returns>
    internal static bool IsNameAlreadyUsed(this ISuiteConnectionService service, Connection connection)
        => service.Connections.Any(c => connection.Id != c.Id && string.Equals(c.Name, connection.Name, StringComparison.Ordinal));
}
