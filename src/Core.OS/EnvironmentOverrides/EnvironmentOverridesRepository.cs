using System.IO.Abstractions;
using System.Text;
using Core.OS.Instance;
using Microsoft.Extensions.Options;
using Sdk.Backend.IO;

namespace Core.OS.EnvironmentOverrides;

internal sealed partial class EnvironmentOverridesRepository(
    IFileSystem fileSystem,
    IAtomicFileWriter atomicFileWriter,
    IOptions<InstanceOptions> instanceOptions,
    ILogger<EnvironmentOverridesRepository> logger)
    : IEnvironmentOverridesRepository
{
    public async Task<IReadOnlyList<KeyValuePair<string, string>>> Get(CancellationToken cancellationToken = default)
    {
        var path = RequirePath();
        if (!fileSystem.File.Exists(path))
            return [];

        var contents = await fileSystem.File.ReadAllTextAsync(path, cancellationToken);
        return EnvironmentOverridesFormat.Parse(contents);
    }

    public async Task Store(IReadOnlyList<KeyValuePair<string, string>> overrides,
        CancellationToken cancellationToken = default)
    {
        AssertOnlyValidKeys(overrides);
        EnsureValidValues(overrides);

        var path = RequirePath();
        var contents = EnvironmentOverridesFormat.Serialize(overrides);

        await atomicFileWriter.WriteAsync(path,
            stream => stream.WriteAsync(Encoding.UTF8.GetBytes(contents), cancellationToken).AsTask(),
            cancellationToken);

        LogStoredCountEnvironmentOverridesKeys(overrides.Count, overrides.Select(o => o.Key));
    }

    private string RequirePath()
        => EnvironmentOverridesFile.RequirePath(fileSystem, instanceOptions.Value.HomeDirectory);

    // The whole batch is refused rather than the offending entry dropped: the caller submits a
    // complete desired state, so a partial write would persist something nobody asked for.
    private static void AssertOnlyValidKeys(IReadOnlyList<KeyValuePair<string, string>> overrides)
    {
        var invalidKey = overrides
            .Select(o => o.Key)
            .FirstOrDefault(key => !EnvironmentOverridesFile.IsValidKey(key));

        if (invalidKey is not null)
            throw new ArgumentException($"'{invalidKey}' is not a valid environment variable name.",
                nameof(overrides));
    }

    // Names the offending key but never its value: values are where secrets land, which is also
    // why the stored-overrides log line records keys only.
    private static void EnsureValidValues(IReadOnlyList<KeyValuePair<string, string>> overrides)
    {
        var keyWithInvalidValue = overrides
            .Where(o => !EnvironmentOverridesFile.IsValidValue(o.Value))
            .Select(o => o.Key)
            .FirstOrDefault();

        if (keyWithInvalidValue is not null)
            throw new ArgumentException(
                $"The value of '{keyWithInvalidValue}' cannot be stored: it contains a NUL character.",
                nameof(overrides));
    }

    [LoggerMessage(LogLevel.Information, "Stored {Count} environment overrides: {Keys}")]
    partial void LogStoredCountEnvironmentOverridesKeys(int count, IEnumerable<string> keys);
}
