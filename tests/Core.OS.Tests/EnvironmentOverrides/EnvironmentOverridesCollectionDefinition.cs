namespace Core.OS.Tests.EnvironmentOverrides;

/// <summary>
/// Runs the override tests one after another: they switch the feature on and off through the
/// process environment, which every test in the assembly shares.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EnvironmentOverridesCollectionDefinition
{
    public const string Name = "EnvironmentOverrides";
}
