using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sdk.Backend.ArtifactApi;
using Sdk.Messaging;

namespace Core.Module.JFrog;

/// <inheritdoc />
internal class JFrogArtifactQueryBuilder(string repository) : IArtifactQueryBuilder
{
    private readonly AqlQuery _query = new() { Repository = repository };
    private List<string>? _includes;
    private object? _sort;

    /// <summary>
    /// adds "path":{"$match":"expression"} to $and list
    /// </summary>
    /// <param name="expression">e.g. /some/path/to or /some/path*</param>
    public IArtifactQueryBuilder AndPathMatches(string expression)
    {
        _query.Criteria ??= [];

        var match = new AqlMatchExpression { Expression = expression };
        var pathMatch = new AqlPathMatch { Match = match };

        _query.Criteria.Add(pathMatch);

        return this;
    }

    /// <summary>
    /// adds "name":{"$match":"expression"}
    /// </summary>
    /// <param name="expression">e.g. NameXyz or NameXy*</param>
    public IArtifactQueryBuilder AndNameMatches(string expression)
    {
        _query.Criteria ??= [];

        var match = new AqlMatchExpression { Expression = expression };
        var pathMatch = new AqlNameMatch { Match = match };

        _query.Criteria.Add(pathMatch);

        return this;
    }

    /// <summary>
    /// adds "name":{"$match":"expression"}
    /// </summary>
    /// <param name="expression">e.g. NameXyz or NameXy*</param>
    /// <returns></returns>
    public IArtifactQueryBuilder AndNameNotMatches(string expression)
    {
        _query.Criteria ??= [];

        var match = new AqlNotMatchExpression { Expression = expression };
        var pathMatch = new AqlNameMatch { Match = match };

        _query.Criteria.Add(pathMatch);

        return this;
    }


    /// <summary>
    /// adds .include("item1", "item2") to query string
    /// </summary>
    public IArtifactQueryBuilder IncludeFields(params string[] includes)
    {
        _includes ??= [];

        _includes.AddRange(includes);

        return this;
    }

    /// <summary>
    /// .sort({"$asc" : ["field1", "field2",... ]})
    /// </summary>
    public IArtifactQueryBuilder OrderBy(params string[] fields)
    {
        _sort = new AqlOrderBy { Fields = [.. fields] };

        return this;
    }

    /// <summary>
    /// .sort({"$desc" : ["field1", "field2",... ]})
    /// </summary>
    public IArtifactQueryBuilder OrderByDescending(params string[] fields)
    {
        _sort = new AqlOrderByDescending { Fields = [.. fields] };

        return this;
    }

    /// <inheritdoc />
    public string BuildQueryString()
    {
        // ensure we don't serialize it if we don't have criteria
        if (_query.Criteria != null && _query.Criteria.Count == 0)
            _query.Criteria = null;

        var sb = new StringBuilder();
        sb.Append("items.find(");
        sb.Append(JsonSerializer.Serialize(_query, DefaultJsonSerializerSettings.Default));
        sb.Append(')');

        // https://jfrog.com/help/r/jfrog-rest-apis/specify-output-fields
        if (_includes is not null)
        {
            sb.Append(".include(");
            sb.Append(string.Join(",", _includes.Select(k => $"\"{k}\"")));
            sb.Append(')');
        }

        // https://jfrog.com/help/r/jfrog-rest-apis/sorting-in-aql
        if (_sort is not null)
        {
            sb.Append(".sort(");
            sb.Append(JsonSerializer.Serialize(_sort));
            sb.Append(')');
        }

        return sb.ToString();
    }

    internal class AqlQuery
    {
        [JsonPropertyName("repo")]
        public required string Repository { get; set; }

        [JsonPropertyName("$and")]
        public List<object>? Criteria { get; set; }
    }

    internal class AqlPathMatch
    {
        [JsonPropertyName("path")]
        public required AqlMatchExpression Match { get; set; }
    }

    internal class AqlNameMatch
    {
        [JsonPropertyName("name")]
        public required object Match { get; set; }
    }

    internal class AqlMatchExpression
    {
        [JsonPropertyName("$match")]
        public required string Expression { get; set; }
    }

    internal class AqlNotMatchExpression
    {
        [JsonPropertyName("$nmatch")]
        public required string Expression { get; set; }
    }

    internal class AqlOrderBy
    {
        [JsonPropertyName("$asc")]
        public required List<string> Fields { get; set; }
    }

    internal class AqlOrderByDescending
    {
        [JsonPropertyName("$desc")]
        public required List<string> Fields { get; set; }
    }
}
