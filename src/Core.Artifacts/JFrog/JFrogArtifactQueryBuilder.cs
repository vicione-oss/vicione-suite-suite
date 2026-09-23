using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sdk.Backend.Artifacts;
using Sdk.Messaging;

namespace Core.Artifacts.JFrog;

/// <inheritdoc />
internal class JFrogArtifactQueryBuilder : IArtifactQueryBuilder
{
    public const string RepositoryPlaceholder = "__repository__";

    private readonly AqlQuery _query = new();
    private List<string>? _includes;
    private object? _sort;
    private object? _typeFilter;
    private object? _modifiedAfterFilter;
    private int? _limit;
    private int? _offset;

    /// <summary>
    /// Adds "path":{"$match":"expression"} to the $and list, e.g. /some/path/to or /some/path*.
    /// </summary>
    public IArtifactQueryBuilder AndPathMatches(string expression)
    {
        _query.Criteria ??= [];

        var match = new AqlMatchExpression { Expression = expression };
        var pathMatch = new AqlPathMatch { Match = match };

        _query.Criteria.Add(pathMatch);

        return this;
    }

    /// <summary>
    /// Adds "name":{"$match":"expression"} to the $and list, e.g. NameXyz or NameXy*.
    /// </summary>
    public IArtifactQueryBuilder AndNameMatches(string expression)
    {
        _query.Criteria ??= [];

        var match = new AqlMatchExpression { Expression = expression };
        var pathMatch = new AqlNameMatch { Match = match };

        _query.Criteria.Add(pathMatch);

        return this;
    }

    /// <summary>
    /// Adds "name":{"$nmatch":"expression"} to the $and list, e.g. NameXyz or NameXy*.
    /// </summary>
    public IArtifactQueryBuilder AndNameNotMatches(string expression)
    {
        _query.Criteria ??= [];

        var match = new AqlNotMatchExpression { Expression = expression };
        var pathMatch = new AqlNameMatch { Match = match };

        _query.Criteria.Add(pathMatch);

        return this;
    }

    /// <summary>
    /// Adds "modified":{"$gt":"datetime:O"}.
    /// </summary>
    public IArtifactQueryBuilder ModifiedAfter(DateTimeOffset value)
    {
        // https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings#Roundtrip
        var formattedDate = value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var expression = new AqlGreaterThanExpression { Expression = formattedDate };
        _modifiedAfterFilter = new AqlModifiedAfter { After = expression };

        return this;
    }

    /// <summary>
    /// Adds .include("item1", "item2") to the query string.
    /// </summary>
    public IArtifactQueryBuilder IncludeFields(params string[] includes)
    {
        _includes ??= [];

        _includes.AddRange(includes);

        return this;
    }

    /// <summary>
    /// Adds .sort({"$asc" : ["field1", "field2", ...]}).
    /// </summary>
    public IArtifactQueryBuilder OrderBy(params string[] fields)
    {
        _sort = new AqlOrderBy { Fields = [.. fields] };

        return this;
    }

    /// <summary>
    /// Adds .sort({"$desc" : ["field1", "field2", ...]}).
    /// </summary>
    public IArtifactQueryBuilder OrderByDescending(params string[] fields)
    {
        _sort = new AqlOrderByDescending { Fields = [.. fields] };

        return this;
    }

    /// <summary>
    /// Adds .find("type" : "folder"). Files are the JFrog default and need no filter.
    /// </summary>
    public IArtifactQueryBuilder FilterBy(ArtifactKind artifactType)
    {
        if (artifactType == ArtifactKind.Folder)
        {
            _typeFilter = new AqlTypeMatch { Type = "folder" };
        }

        return this;
    }

    /// <summary>
    /// Adds .offset(100).limit(50).
    /// </summary>
    public IArtifactQueryBuilder Limit(int itemLimit, int offset = 0)
    {
        _limit = itemLimit;
        _offset = offset;

        return this;
    }

    /// <inheritdoc />
    public string Build()
    {
        // An empty criteria list must not reach the serializer.
        if (_query.Criteria != null && _query.Criteria.Count == 0)
            _query.Criteria = null;

        var sb = new StringBuilder();
        sb.Append("items.find(");

        if (_typeFilter is not null)
        {
            sb.Append(JsonSerializer.Serialize(_typeFilter, DefaultJsonSerializerSettings.Default));
            sb.Append(',');
        }

        if (_modifiedAfterFilter is not null)
        {
            sb.Append(JsonSerializer.Serialize(_modifiedAfterFilter, DefaultJsonSerializerSettings.Default));
            sb.Append(',');
        }

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

        // https://jfrog.com/help/r/jfrog-rest-apis/display-limits-and-pagination
        if (_offset is > 0)
            sb.Append(CultureInfo.InvariantCulture, $".offset({_offset})");

        if (_limit is > 0)
            sb.Append(CultureInfo.InvariantCulture, $".limit({_limit})");

        return sb.ToString();
    }

    /// <summary>
    /// Replaces <see cref="RepositoryPlaceholder"/> in <paramref name="aqlQuery"/> with <paramref name="repository"/>.
    /// </summary>
    public static string InjectRepository(string aqlQuery, string repository)
    {
        // Injected here to keep the multiple configured sources out of the public API. With several
        // repositories on one host, e.g. igm.jfrog.io, this filter is the only way to select the
        // matching artifacts; exposing the AQL on the API would be the better fix.
        return aqlQuery.Replace(RepositoryPlaceholder, repository, StringComparison.Ordinal);
    }

    internal class AqlQuery
    {
        [JsonPropertyName("repo")]
        public string Repository { get; set; } = RepositoryPlaceholder;

        [JsonPropertyName("$and")]
        public List<object>? Criteria { get; set; }
    }

    internal class AqlTypeMatch
    {
        [JsonPropertyName("type")]
        public required string Type { get; set; }
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

    internal class AqlModifiedAfter
    {
        [JsonPropertyName("modified")]
        public required AqlGreaterThanExpression After { get; set; }
    }

    internal class AqlGreaterThanExpression
    {
        [JsonPropertyName("$gt")]
        public required string Expression { get; set; }
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
