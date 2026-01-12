using Sdk.Backend.ArtifactApi;

namespace Core.Module.Nexus;

public class NexusApiQueryBuilder : IArtifactQueryBuilder
{
    private readonly List<string> _matchNames = [];
    private readonly List<string> _matchPaths = [];
    private readonly List<string> _orderByFields = [];
    private readonly List<string> _orderByDescFields = [];

    public IArtifactQueryBuilder AndNameMatches(string pattern)
    {
        _matchNames.Add(pattern);

        return this;
    }

    public IArtifactQueryBuilder AndNameNotMatches(string pattern)
    {
        _matchNames.Add(pattern);
        return this;
    }

    public IArtifactQueryBuilder AndPathMatches(string pattern)
    {
        _matchPaths.Add(pattern);
        return this;
    }

    public string BuildQueryString()
    {
        var queryParams = new List<KeyValuePair<string, string>>();
        var queryPrefix = string.Empty;
        var path = _matchPaths.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(path))
        {
            queryPrefix = $"/{path.Trim('/')}/";
        }

        // we can't support only a single name
        var name = _matchNames.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(name))
            queryParams.Add(CreateNameFilterParam(name));

        return $"{queryPrefix}{string.Join("&", queryParams.Select(k => $"{k.Key}={k.Value}"))}";
    }

    public IArtifactQueryBuilder IncludeFields(params string[] includes)
    {
        // we can't do it
        return this;
    }

    public IArtifactQueryBuilder OrderBy(params string[] fields)
    {
        // these are really a problem because nexus does not support sorting
        // we would have to use these things later on in the api implementation
        _orderByFields.AddRange(fields);
        return this;
    }

    public IArtifactQueryBuilder OrderByDescending(params string[] fields)
    {
        _orderByDescFields.AddRange(fields);
        return this;
    }

    private static KeyValuePair<string, string> CreateNameFilterParam(string nameFilter)
        => new("name", nameFilter);
}
