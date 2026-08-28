namespace Core.OS.EnvironmentOverrides;

public interface IEnvironmentOverridesRepository
{
    Task<IReadOnlyDictionary<string, string>> Get(CancellationToken cancellationToken = default);

    Task Store(IReadOnlyDictionary<string, string> overrides, CancellationToken cancellationToken = default);
}
