using AwesomeAssertions;
using Blazor.Shared.NavTiles.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Authorization;
using Sdk.Client.Modules;
using Sdk.Client.NavTiles.Attributes;
using Sdk.Client.NavTiles.Components;
using Sdk.Client.NavTiles.Enums;
using Sdk.Client.NavTiles.Extensions;
using Sdk.Client.NavTiles.Services;
using Sdk.Modules;
using Xunit;

namespace Blazor.Shared.Tests.NavTiles;

public sealed class NavTileRegistryTests
{
    [Fact]
    public void Should_be_resolvable_per_module()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNavTilesInfrastructure()
            .AddNavTiles<TestClientModuleA>();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var navTileRegistry = serviceProvider.GetService<INavTileRegistry<TestClientModuleA>>();

        // Assert
        navTileRegistry.Should().NotBeNull();
    }

    [Fact]
    public void Should_be_resolvable_for_all_modules()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNavTilesInfrastructure()
            .AddNavTiles<TestClientModuleA>()
            .AddNavTiles<TestClientModuleB>();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var navTileRegistries = serviceProvider.GetRequiredService<IEnumerable<INavTileRegistry<IClientModule>>>().ToList();

        // Assert
        navTileRegistries.Should().NotBeNull().And.HaveCount(2);
        navTileRegistries.First().Should().BeAssignableTo<INavTileRegistry<TestClientModuleA>>();
        navTileRegistries.Skip(1).First().Should().BeAssignableTo<INavTileRegistry<TestClientModuleB>>();
    }

    [Fact]
    public void Should_discover_nav_tile_on_instantiation()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNavTilesInfrastructure()
            .AddNavTiles<TestClientModuleA>();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var navTileRegistry = serviceProvider.GetService<INavTileRegistry<TestClientModuleA>>();

        // Assert
        navTileRegistry.Should().HaveCount(1);
        navTileRegistry.Should().Contain(i => i.ComponentType == typeof(TestClientModuleA.NavTile));
    }

    [Fact]
    public void Assert_registered_nav_tile_parameters_of_discovered_nav_tile()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNavTilesInfrastructure()
            .AddNavTiles<TestClientModuleA>();

        var serviceProvider = services.BuildServiceProvider();

        var navTileRegistry = serviceProvider.GetRequiredService<INavTileRegistry<TestClientModuleA>>();

        // Act
        var result = navTileRegistry.First(i => i.ComponentType == typeof(TestClientModuleA.NavTile));

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(TestClientModuleA.NavTile.DefaultId);
        result.ComponentType.Should().Be<TestClientModuleA.NavTile>();
        result.State.Enabled.Should().Be(TestClientModuleA.NavTile.DefaultEnabled);
        result.State.LinkTarget.Should().Be(TestClientModuleA.NavTile.DefaultLinkTarget);
        result.State.HorizontalSpan.Should().Be(TestClientModuleA.NavTile.DefaultHorizontalSpan);
        result.Group.Should().Be(TestClientModuleA.NavTile.DefaultGroup);
        result.AuthorizationRequirement.Should().BeEquivalentTo(new AccessLevelAuthorizationRequirement(TestClientModuleA.ModuleId, TestClientModuleA.NavTile.DefaultAccessLevel));
    }

    [Fact]
    public void Assert_registered_nav_tile_parameters_after_add_operation()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNavTilesInfrastructure()
            .AddNavTiles<TestClientModuleA>();

        var serviceProvider = services.BuildServiceProvider();

        var navTileRegistry = serviceProvider.GetRequiredService<INavTileRegistry<TestClientModuleA>>();

        var navTileId = Guid.NewGuid().ToString();
        var horizontalSpan = NavTileSpan.Two;
        var enabled = false;
        var linkTarget = "/foo";
        var group = NavTileGroup.Favorites;
        var authorizationRequirement = new AccessLevelAuthorizationRequirement(TestClientModuleA.ModuleId, AccessLevel.Partial);

        // Act
        var result = navTileRegistry.Add<TestClientModuleA.NavTile>(navTileId, horizontalSpan, enabled, linkTarget, group, authorizationRequirement);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(navTileId);
        result.ComponentType.Should().Be<TestClientModuleA.NavTile>();
        result.State.Enabled.Should().Be(enabled);
        result.State.LinkTarget.Should().Be(linkTarget);
        result.State.HorizontalSpan.Should().Be(horizontalSpan);
        result.Group.Should().Be(group);
        result.AuthorizationRequirement.Should().Be(authorizationRequirement);
    }

    [Fact]
    public void Should_trigger_changed_event_on_add_operation()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNavTilesInfrastructure()
            .AddNavTiles<TestClientModuleA>();

        var serviceProvider = services.BuildServiceProvider();

        var navTileRegistryChanged = false;

        var navTileRegistry = serviceProvider.GetRequiredService<INavTileRegistry<TestClientModuleA>>();
        navTileRegistry.Changed += () => navTileRegistryChanged = true;

        var navTileId = Guid.NewGuid().ToString();

        // Act
        navTileRegistry.Add<TestClientModuleA.NavTile>(navTileId);

        // Assert
        navTileRegistryChanged.Should().BeTrue();
    }

    [Fact]
    public void Should_trigger_changed_event_on_remove_operation()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNavTilesInfrastructure()
            .AddNavTiles<TestClientModuleA>();

        var serviceProvider = services.BuildServiceProvider();

        var navTileRegistryChanged = false;

        var navTileRegistry = serviceProvider.GetRequiredService<INavTileRegistry<TestClientModuleA>>();
        navTileRegistry.Changed += () => navTileRegistryChanged = true;

        // Act
        var navTileId = navTileRegistry.First().Id;

        navTileRegistry.Remove(navTileId);

        // Assert
        navTileRegistryChanged.Should().BeTrue();
    }

    public sealed class TestClientModuleA : IClientModule
    {
        public const string ModuleId = "TestClientModuleA";

        public ModuleKey ModuleKey => new() { ModuleId = ModuleId };

        [InitialNavTile<TestClientModuleA>(Id = DefaultId,
            HorizontalSpan = DefaultHorizontalSpan,
            Enabled = DefaultEnabled,
            LinkTarget = DefaultLinkTarget,
            Group = NavTileGroup.Favorites)]
        [ModuleAuthorize(moduleId: ModuleId, accessLevel: DefaultAccessLevel)]
        public sealed class NavTile : ComponentBase, INavTile
        {
            internal const string DefaultId = "e149a029-620e-42e4-9c44-aa8b602194a8";
            internal const NavTileSpan DefaultHorizontalSpan = NavTileSpan.Two;
            internal const bool DefaultEnabled = false;
            internal const string DefaultLinkTarget = "/foo";
            internal const NavTileGroup DefaultGroup = NavTileGroup.Favorites;
            internal const AccessLevel DefaultAccessLevel = AccessLevel.Full;
        }
    }

    public sealed class TestClientModuleB : IClientModule
    {
        public ModuleKey ModuleKey => new();
    }
}
