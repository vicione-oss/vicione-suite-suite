using Xunit;

namespace Core.OS.E2E.Tests.Infrastructure;

/// <summary>
/// Groups all end-to-end tests into a single collection so they share one browser
/// (<see cref="PlaywrightFixture"/>) and run serially against the single Suite instance
/// under test. Test classes opt in with <c>[Collection(E2ECollectionDefinition.Name)]</c>.
/// </summary>
[CollectionDefinition(Name)]
public sealed class E2ECollectionDefinition : ICollectionFixture<PlaywrightFixture>
{
    public const string Name = "Suite E2E";
}
