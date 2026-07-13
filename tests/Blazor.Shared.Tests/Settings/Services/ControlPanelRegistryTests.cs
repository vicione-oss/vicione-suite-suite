using AwesomeAssertions;
using Blazor.Shared.Settings.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Extensions;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Modules;
using Sdk.Modules;
using Xunit;

namespace Blazor.Shared.Tests.Settings.Services;

public sealed class ControlPanelRegistryTests
{
    [Fact]
    public void Should_be_resolvable_per_module()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddControlPanelInfrastructure();
        services.AddControlPanel<TestClientModuleA, TestClientModuleA.ThemeControlPanel, ControlPanelState>()
            .WithAutoDiscovery<TestClientModuleA.ThemeControlPanelDescriptor>();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var controlPanelRegistry = serviceProvider.GetService<IControlPanelRegistry<TestClientModuleA>>();

        // Assert
        controlPanelRegistry.Should().NotBeNull();
    }

    [Fact]
    public void Should_be_resolvable_for_all_modules()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddControlPanelInfrastructure();
        services.AddControlPanel<TestClientModuleA, TestClientModuleA.ThemeControlPanel, ControlPanelState>()
            .WithAutoDiscovery<TestClientModuleA.ThemeControlPanelDescriptor>();

        services.AddControlPanel<TestClientModuleB, TestClientModuleB.ControlPanel, ControlPanelState>()
            .WithAutoDiscovery<TestClientModuleB.ControlPanelDescriptor>();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var controlPanelRegistries = serviceProvider.GetRequiredService<IEnumerable<IControlPanelRegistry>>().ToList();

        // Assert
        controlPanelRegistries.Should().NotBeNull().And.HaveCount(2);
        controlPanelRegistries.First().Should().BeAssignableTo<IControlPanelRegistry<TestClientModuleA>>();
        controlPanelRegistries.Skip(1).First().Should().BeAssignableTo<IControlPanelRegistry<TestClientModuleB>>();
    }

    [Fact]
    public void Should_discover_control_panel_on_instantiation()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddControlPanelInfrastructure();
        services.AddControlPanel<TestClientModuleA, TestClientModuleA.GeneralControlPanel, TestClientModuleA.GeneralControlPanelState>()
            .WithAutoDiscovery<TestClientModuleA.GeneralControlPanelDescriptor>();

        services.AddControlPanel<TestClientModuleA, TestClientModuleA.ThemeControlPanel, ControlPanelState>()
            .WithAutoDiscovery<TestClientModuleA.ThemeControlPanelDescriptor>();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var controlPanelRegistry = serviceProvider.GetService<IControlPanelRegistry<TestClientModuleA>>();

        // Assert
        controlPanelRegistry.Should().HaveCount(2);
        controlPanelRegistry.Should().Contain(i => i.ComponentType == typeof(TestClientModuleA.GeneralControlPanel));
        controlPanelRegistry.Should().NotContain(i => i.ComponentType == typeof(TestClientModuleA.AppearanceControlPanel));
        controlPanelRegistry.Should().Contain(i => i.ComponentType == typeof(TestClientModuleA.ThemeControlPanel));
    }

    [Fact]
    public void Should_support_attributes_and_manual_di_registration_at_the_same_time()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddControlPanelInfrastructure();
        services.AddControlPanel<TestClientModuleA, TestClientModuleA.GeneralControlPanel, TestClientModuleA.GeneralControlPanelState>()
            .WithAutoDiscovery<TestClientModuleA.GeneralControlPanelDescriptor>();

        services.AddControlPanel<TestClientModuleA, TestClientModuleA.ThemeControlPanel, ControlPanelState>()
            .WithAutoDiscovery<TestClientModuleA.ThemeControlPanelDescriptor>();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var controlPanelRegistries = serviceProvider.GetService<IEnumerable<IControlPanelRegistry>>();

        // Assert
        controlPanelRegistries.Should().HaveCount(1);

        var firstControlPanelRegistry = controlPanelRegistries!.First();
        firstControlPanelRegistry.Should().HaveCount(2);
    }

    [Fact]
    public void Should_have_registered_control_panel_parameters_of_control_panel_discovered_from_di_registration()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddControlPanelInfrastructure();
        services.AddControlPanel<TestClientModuleA, TestClientModuleA.ThemeControlPanel, ControlPanelState>()
            .WithAutoDiscovery<TestClientModuleA.ThemeControlPanelDescriptor>();

        var serviceProvider = services.BuildServiceProvider();

        var controlPanelRegistry = serviceProvider.GetRequiredService<IControlPanelRegistry<TestClientModuleA>>();

        // Act
        var result = controlPanelRegistry.First();

        // Assert
        result.Should().NotBeNull();
        result.ComponentType.Should().Be<TestClientModuleA.ThemeControlPanel>();
        result.Descriptor.GetType().Should().Be<TestClientModuleA.ThemeControlPanelDescriptor>();
        result.CategoryDescriptor.GetType().Should().Be<TestClientModuleA.ControlPanelCategoryDescriptor>();
        result.GroupDescriptor.GetType().Should().Be<TestClientModuleA.ControlPanelGroupDescriptor>();
        result.State.GetType().Should().Be<ControlPanelState>();
        result.AuthorizationRequirement.Should().BeEquivalentTo(new AccessLevelAuthorizationRequirement(TestClientModuleA.ModuleId, TestClientModuleA.ThemeControlPanel.DefaultAccessLevel));
    }

    [Fact]
    public void Should_have_registered_control_panel_parameters_after_add_operation()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddControlPanelInfrastructure();
        services.AddControlPanelCore<TestClientModuleA>();

        var serviceProvider = services.BuildServiceProvider();

        var controlPanelRegistry = serviceProvider.GetRequiredService<IControlPanelRegistry<TestClientModuleA>>();
        var controlPanelDescriptor = new TestClientModuleA.AppearanceControlPanelDescriptor();
        var controlPanelCategoryDescriptor = new TestClientModuleA.ControlPanelCategoryDescriptor();
        var controlPanelGroupDescriptor = new TestClientModuleA.ControlPanelGroupDescriptor();
        var controlPanelState = new ControlPanelState();
        var controlPanelAuthorizationRequirement = new AccessLevelAuthorizationRequirement(TestClientModuleA.ModuleId, AccessLevel.Partial);

        // Act
        var result = controlPanelRegistry.Add<TestClientModuleA.AppearanceControlPanel, ControlPanelState>(
            controlPanelDescriptor, controlPanelState, controlPanelCategoryDescriptor, controlPanelGroupDescriptor,
            controlPanelAuthorizationRequirement);

        // Assert
        result.Should().NotBeNull();
        result.ComponentType.Should().Be<TestClientModuleA.AppearanceControlPanel>();
        result.Descriptor.Should().Be(controlPanelDescriptor);
        result.CategoryDescriptor.Should().Be(controlPanelCategoryDescriptor);
        result.GroupDescriptor.Should().Be(controlPanelGroupDescriptor);
        result.State.Should().Be(controlPanelState);
        result.AuthorizationRequirement.Should().Be(controlPanelAuthorizationRequirement);
    }

    [Fact]
    public void Should_trigger_changed_event_on_add_operation()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddControlPanelInfrastructure();
        services.AddControlPanel<TestClientModuleA, TestClientModuleA.ThemeControlPanel, ControlPanelState>()
            .WithAutoDiscovery<TestClientModuleA.ThemeControlPanelDescriptor>();

        var serviceProvider = services.BuildServiceProvider();

        var controlPanelRegistryChanged = false;

        var controlPanelRegistry = serviceProvider.GetRequiredService<IControlPanelRegistry<TestClientModuleA>>();
        controlPanelRegistry.Changed += args => controlPanelRegistryChanged = true;

        var controlPanelDescriptor = new TestClientModuleA.AppearanceControlPanelDescriptor();
        var controlPanelCategoryDescriptor = new TestClientModuleA.ControlPanelCategoryDescriptor();
        var controlPanelState = new ControlPanelState();

        // Act
        controlPanelRegistry.Add<TestClientModuleA.AppearanceControlPanel, ControlPanelState>(controlPanelDescriptor,
            controlPanelState, controlPanelCategoryDescriptor);

        // Assert
        controlPanelRegistryChanged.Should().BeTrue();
    }

    [Fact]
    public void Should_trigger_changed_event_on_remove_by_item_operation()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddControlPanelInfrastructure();
        services.AddControlPanel<TestClientModuleA, TestClientModuleA.GeneralControlPanel, TestClientModuleA.GeneralControlPanelState>()
            .WithAutoDiscovery<TestClientModuleA.GeneralControlPanelDescriptor>();

        var serviceProvider = services.BuildServiceProvider();

        var controlPanelRegistryChanged = false;

        var controlPanelRegistry = serviceProvider.GetRequiredService<IControlPanelRegistry<TestClientModuleA>>();
        controlPanelRegistry.Changed += args => controlPanelRegistryChanged = true;

        // Act
        var controlPanelRegistryItem = controlPanelRegistry.First();

        var controlPanelRemoved = controlPanelRegistry.Remove(controlPanelRegistryItem);

        // Assert
        controlPanelRegistryChanged.Should().BeTrue();
        controlPanelRemoved.Should().Be(true);
    }

    [Fact]
    public void Should_trigger_changed_event_on_remove_by_class_operation()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddControlPanelInfrastructure();
        services.AddControlPanel<TestClientModuleA, TestClientModuleA.GeneralControlPanel, TestClientModuleA.GeneralControlPanelState>()
            .WithAutoDiscovery<TestClientModuleA.GeneralControlPanelDescriptor>();

        var serviceProvider = services.BuildServiceProvider();

        var controlPanelRegistryChanged = false;

        var controlPanelRegistry = serviceProvider.GetRequiredService<IControlPanelRegistry<TestClientModuleA>>();
        controlPanelRegistry.Changed += args => controlPanelRegistryChanged = true;

        // Act
        var controlPanelsRemoved = controlPanelRegistry.Remove<TestClientModuleA.GeneralControlPanel>();

        // Assert
        controlPanelRegistryChanged.Should().BeTrue();
        controlPanelsRemoved.Should().Be(1);
    }

    [Fact]
    public void Should_trigger_changed_event_on_remove_by_predicate_operation()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddControlPanelInfrastructure();
        services.AddControlPanel<TestClientModuleA, TestClientModuleA.GeneralControlPanel, TestClientModuleA.GeneralControlPanelState>()
            .WithAutoDiscovery<TestClientModuleA.GeneralControlPanelDescriptor>();

        var serviceProvider = services.BuildServiceProvider();

        var controlPanelRegistryChanged = false;

        var controlPanelRegistry = serviceProvider.GetRequiredService<IControlPanelRegistry<TestClientModuleA>>();
        controlPanelRegistry.Changed += args => controlPanelRegistryChanged = true;

        // Act
        var controlPanelsRemoved = controlPanelRegistry.Remove(t => t.ComponentType.IsAssignableTo(typeof(TestClientModuleA.GeneralControlPanel)));

        // Assert
        controlPanelRegistryChanged.Should().BeTrue();
        controlPanelsRemoved.Should().Be(1);
    }

    [Fact]
    public void Should_not_trigger_changed_event_after_begin_update()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddControlPanelInfrastructure();
        services.AddControlPanel<TestClientModuleA, TestClientModuleA.ThemeControlPanel, ControlPanelState>()
            .WithAutoDiscovery<TestClientModuleA.ThemeControlPanelDescriptor>();

        var serviceProvider = services.BuildServiceProvider();

        var controlPanelRegistryChanged = false;

        var controlPanelRegistry = serviceProvider.GetRequiredService<IControlPanelRegistry<TestClientModuleA>>();
        controlPanelRegistry.Changed += args => controlPanelRegistryChanged = true;

        // Act
        controlPanelRegistry.BeginUpdate();

        while (controlPanelRegistry.Any())
        {
            var controlPanelRegistryItem = controlPanelRegistry.First();

            controlPanelRegistry.Remove(controlPanelRegistryItem);
        }

        controlPanelRegistry.Add<TestClientModuleA.GeneralControlPanel, TestClientModuleA.GeneralControlPanelState>(
            new TestClientModuleA.GeneralControlPanelDescriptor(),
            new TestClientModuleA.GeneralControlPanelState(),
            new TestClientModuleA.ControlPanelCategoryDescriptor());

        // Assert
        controlPanelRegistryChanged.Should().Be(false);
    }

    [Fact]
    public void Should_trigger_changed_event_on_end_update_with_correct_args()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddControlPanelInfrastructure();
        services.AddControlPanel<TestClientModuleA, TestClientModuleA.GeneralControlPanel, TestClientModuleA.GeneralControlPanelState>()
            .WithAutoDiscovery<TestClientModuleA.GeneralControlPanelDescriptor>();

        services.AddControlPanel<TestClientModuleA, TestClientModuleA.ThemeControlPanel, ControlPanelState>()
            .WithAutoDiscovery<TestClientModuleA.ThemeControlPanelDescriptor>();

        var serviceProvider = services.BuildServiceProvider();

        var registryChangedCounter = 0;
        var itemsRemoved = 0;
        var itemsAdded = 0;

        var controlPanelRegistry = serviceProvider.GetRequiredService<IControlPanelRegistry<TestClientModuleA>>();
        controlPanelRegistry.Changed += args =>
        {
            registryChangedCounter++;
            itemsRemoved = args.ItemsRemoved.Count();
            itemsAdded = args.ItemsAdded.Count();
        };

        // Act
        controlPanelRegistry.BeginUpdate();
        try
        {
            var firstRegistryItem = controlPanelRegistry.First();
            var secondRegistryItem = controlPanelRegistry.Skip(1).First();

            controlPanelRegistry.Remove(firstRegistryItem);
            controlPanelRegistry.Remove(secondRegistryItem);

            controlPanelRegistry.Add<TestClientModuleA.GeneralControlPanel, TestClientModuleA.GeneralControlPanelState>(
                new TestClientModuleA.GeneralControlPanelDescriptor(),
                new TestClientModuleA.GeneralControlPanelState(),
                new TestClientModuleA.ControlPanelCategoryDescriptor());
        }
        finally
        {
            controlPanelRegistry.EndUpdate();
        }

        // Assert
        registryChangedCounter.Should().Be(1);
        itemsRemoved.Should().Be(2);
        itemsAdded.Should().Be(1);
    }

    [Fact]
    public async Task Should_trigger_changed_event_on_outer_end_update()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddControlPanelInfrastructure();
        services.AddControlPanel<TestClientModuleB, TestClientModuleB.ControlPanel, ControlPanelState>()
            .WithAutoDiscovery<TestClientModuleB.ControlPanelDescriptor>();

        var serviceProvider = services.BuildServiceProvider();

        var controlPanelRegistryChangedCounter = 0;

        var controlPanelRegistry = serviceProvider.GetRequiredService<IControlPanelRegistry<TestClientModuleB>>();
        controlPanelRegistry.Changed += args => ++controlPanelRegistryChangedCounter;

        static async Task RandomUpdateTask(Random random, IControlPanelRegistry<TestClientModuleB> controlPanelRegistry)
        {
            var delay = random.Next(0, 100);
            await Task.Delay(delay);

            controlPanelRegistry.BeginUpdate();
            try
            {
                var coinToss = random.Next(0, 2);

                if (coinToss == 0)
                {
                    var controlPanelRegistryItem = controlPanelRegistry.FirstOrDefault();
                    if (controlPanelRegistryItem is not null)
                        controlPanelRegistry.Remove(controlPanelRegistryItem);
                    else
                        coinToss = 1;
                }

                if (coinToss == 1)
                {
                    controlPanelRegistry.Add<TestClientModuleB.ControlPanel, ControlPanelState>(
                        new TestClientModuleB.ControlPanelDescriptor(),
                        new ControlPanelState(),
                        new TestClientModuleB.ControlPanelCategoryDescriptor());
                }
            }
            finally
            {
                controlPanelRegistry.EndUpdate();
            }
        }

        // Act
        controlPanelRegistry.BeginUpdate();
        try
        {
            var random = new Random();

            var updateTasks = new List<Task>();
            for (var i = 0; i < 1000; i++)
                updateTasks.Add(RandomUpdateTask(random, controlPanelRegistry));

            await Task.WhenAll(updateTasks);
        }
        finally
        {
            controlPanelRegistry.EndUpdate();
        }

        // Assert
        controlPanelRegistryChangedCounter.Should().Be(1);
    }

    [Fact]
    public void Should_increase_update_lock_on_begin_update()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddControlPanelInfrastructure();
        services.AddControlPanelCore<TestClientModuleA>();

        var serviceProvider = services.BuildServiceProvider();

        var controlPanelRegistry = serviceProvider.GetRequiredService<IControlPanelRegistry<TestClientModuleA>>();

        // Act
        controlPanelRegistry.BeginUpdate();

        // Assert
        controlPanelRegistry.UpdateLock.Should().Be(1);
    }

    [Fact]
    public void Should_decrease_update_lock_on_end_update()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddControlPanelInfrastructure();
        services.AddControlPanelCore<TestClientModuleA>();

        var serviceProvider = services.BuildServiceProvider();

        var controlPanelRegistry = serviceProvider.GetRequiredService<IControlPanelRegistry<TestClientModuleA>>();

        // Act
        controlPanelRegistry.BeginUpdate();
        controlPanelRegistry.EndUpdate();

        // Assert
        controlPanelRegistry.UpdateLock.Should().Be(0);
    }

    public sealed class TestClientModuleA : IClientModule
    {
        public const string ModuleId = "TestClientModuleA";

        public ModuleKey ModuleKey => new() { ModuleId = ModuleId };

        [ControlPanelCategory<ControlPanelCategoryDescriptor>]
        [ControlPanelGroup<ControlPanelGroupDescriptor>]
        [ModuleAuthorize(moduleId: ModuleId, accessLevel: DefaultAccessLevel)]
        public sealed class GeneralControlPanel : ControlPanelBase<GeneralControlPanelState>
        {
            internal const AccessLevel DefaultAccessLevel = AccessLevel.Full;
        }

        public sealed class GeneralControlPanelState : ControlPanelState;

        public sealed class GeneralControlPanelDescriptor : IControlPanelDescriptor<GeneralControlPanel>
        {
            public Uri IconUrl => new("icon.svg", UriKind.Relative);
            public string Title => "General";
        }

        public sealed class AppearanceControlPanel : ControlPanelBase<ControlPanelState>;

        public sealed class AppearanceControlPanelDescriptor : IControlPanelDescriptor<AppearanceControlPanel>
        {
            public Uri IconUrl => new("icon.svg", UriKind.Relative);
            public string Title => "Appearance";
        }

        [ControlPanelCategory<ControlPanelCategoryDescriptor>]
        [ControlPanelGroup<ControlPanelGroupDescriptor>]
        [ModuleAuthorize(moduleId: ModuleId, accessLevel: DefaultAccessLevel)]
        internal sealed class ThemeControlPanel : ControlPanelBase<ControlPanelState>
        {
            internal const AccessLevel DefaultAccessLevel = AccessLevel.Partial;
        }

        internal sealed class ThemeControlPanelDescriptor : IControlPanelDescriptor<ThemeControlPanel>
        {
            public Uri IconUrl => new("icon.svg", UriKind.Relative);
            public string Title => "Theme";
        }

        public sealed class ControlPanelCategoryDescriptor : IControlPanelCategoryDescriptor
        {
            public string? IconCssClass => null;
            public Uri? IconUrl => new("icon.svg", UriKind.Relative);
            public int? Position => null;
            public string Title => "Settings";
        }

        public sealed class ControlPanelGroupDescriptor : IControlPanelGroupDescriptor
        {
            public int Position => 0;
        }
    }

    public sealed class TestClientModuleB : IClientModule
    {
        public ModuleKey ModuleKey => new();

        public sealed class ControlPanel : ControlPanelBase<ControlPanelState>;

        public sealed class ControlPanelDescriptor : IControlPanelDescriptor<ControlPanel>
        {
            public Uri IconUrl => new("icon.svg", UriKind.Relative);
            public string Title => "My sub-category";
        }

        public sealed class ControlPanelCategoryDescriptor : IControlPanelCategoryDescriptor
        {
            public string? IconCssClass => null;
            public Uri? IconUrl => new("icon.svg", UriKind.Relative);
            public int? Position => null;
            public string Title => "My category";
        }
    }
}
