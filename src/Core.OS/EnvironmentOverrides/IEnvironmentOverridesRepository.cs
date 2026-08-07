namespace Core.OS.EnvironmentOverrides;

internal interface IEnvironmentOverridesRepository
{
    Task<IReadOnlyList<KeyValuePair<string, string>>> Get(CancellationToken cancellationToken = default);

    Task Store(IReadOnlyList<KeyValuePair<string, string>> overrides, CancellationToken cancellationToken = default);
}
