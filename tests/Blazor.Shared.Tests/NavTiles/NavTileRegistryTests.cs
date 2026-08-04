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
    public void Should_have_registered_nav_tile_parameters_of_discovered_nav_tile()
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
    public void Should_have_registered_nav_tile_parameters_after_add_operation()
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

    [Fact]
    public void Should_not_trigger_changed_event_after_begin_update()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNavTilesInfrastructure()
            .AddNavTiles<TestClientModuleA>();

        var serviceProvider = services.BuildServiceProvider();

        var navTileRegistry = serviceProvider.GetRequiredService<INavTileRegistry<TestClientModuleA>>();

        var navTileRegistryChanged = false;
        navTileRegistry.Changed += () => navTileRegistryChanged = true;

        // Act
        navTileRegistry.BeginUpdate();

        navTileRegistry.Add<TestClientModuleA.NavTile>(Guid.NewGuid().ToString());

        while (navTileRegistry.Any())
            navTileRegistry.Remove(navTileRegistry.First().Id);

        // Assert
        navTileRegistryChanged.Should().Be(false);
    }

    [Fact]
    public void Should_trigger_changed_event_on_end_update()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNavTilesInfrastructure()
            .AddNavTiles<TestClientModuleA>();

        var serviceProvider = services.BuildServiceProvider();

        var navTileRegistryChangedCounter = 0;

        var navTileRegistry = serviceProvider.GetRequiredService<INavTileRegistry<TestClientModuleA>>();
        navTileRegistry.Changed += () => navTileRegistryChangedCounter++;

        // Act
        navTileRegistry.BeginUpdate();
        try
        {
            navTileRegistry.Add<TestClientModuleA.NavTile>(Guid.NewGuid().ToString());
            navTileRegistry.Remove(navTileRegistry.First().Id);
        }
        finally
        {
            navTileRegistry.EndUpdate();
        }

        // Assert
        navTileRegistryChangedCounter.Should().Be(1);
    }

    [Fact]
    public async Task Should_trigger_changed_event_on_outer_end_update()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNavTilesInfrastructure()
            .AddNavTiles<TestClientModuleA>();

        await using var serviceProvider = services.BuildServiceProvider();

        var navTileRegistryChangedCounter = 0;

        var navTileRegistry = serviceProvider.GetRequiredService<INavTileRegistry<TestClientModuleA>>();
        navTileRegistry.Changed += () => ++navTileRegistryChangedCounter;

        static async Task RandomUpdateTask(Random random, INavTileRegistry<TestClientModuleA> registry)
        {
            var delay = random.Next(0, 100);
            await Task.Delay(delay);

            registry.BeginUpdate();
            try
            {
                var coinToss = random.Next(0, 2);

                if (coinToss == 0)
                {
                    var registryItem = registry.FirstOrDefault();
                    if (registryItem is not null)
                        registry.Remove(registryItem.Id);
                    else
                        coinToss = 1;
                }

                if (coinToss == 1)
                {
                    registry.Add<TestClientModuleA.NavTile>(Guid.NewGuid().ToString());
                }
            }
            finally
            {
                registry.EndUpdate();
            }
        }

        // Act
        navTileRegistry.BeginUpdate();
        try
        {
            var random = new Random();

            var updateTasks = new List<Task>();
            for (var i = 0; i < 1000; i++)
                updateTasks.Add(RandomUpdateTask(random, navTileRegistry));

            await Task.WhenAll(updateTasks);
        }
        finally
        {
            navTileRegistry.EndUpdate();
        }

        // Assert
        navTileRegistryChangedCounter.Should().Be(1);
    }

    [Fact]
    public void Should_increase_update_lock_on_begin_update()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNavTilesInfrastructure()
            .AddNavTiles<TestClientModuleA>();

        var serviceProvider = services.BuildServiceProvider();

        var navTileRegistry = serviceProvider.GetRequiredService<INavTileRegistry<TestClientModuleA>>();

        // Act
        navTileRegistry.BeginUpdate();

        // Assert
        navTileRegistry.UpdateLock.Should().Be(1);
    }

    [Fact]
    public void Should_decrease_update_lock_on_end_update()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNavTilesInfrastructure()
            .AddNavTiles<TestClientModuleA>();

        var serviceProvider = services.BuildServiceProvider();

        var navTileRegistry = serviceProvider.GetRequiredService<INavTileRegistry<TestClientModuleA>>();

        // Act
        navTileRegistry.BeginUpdate();
        navTileRegistry.EndUpdate();

        // Assert
        navTileRegistry.UpdateLock.Should().Be(0);
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
