using Blazor.Shared.NotificationArea.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Authorization;
using Sdk.Client.Modules;
using Sdk.Client.NotificationArea.Attributes;
using Sdk.Client.NotificationArea.Components;
using Sdk.Client.NotificationArea.Extensions;
using Sdk.Client.NotificationArea.Services;
using Sdk.Modules;

namespace Blazor.Shared.Tests.NotificationArea.Services;

public sealed class NotificationElementRegistryTests
{
    [Fact]
    public void Should_be_resolvable_per_module()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNotificationElementInfrastructure()
            .AddNotificationElements<TestClientModuleA>();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var registry = serviceProvider.GetService<INotificationElementRegistry<TestClientModuleA>>();

        // Assert
        registry.Should().NotBeNull();
    }

    [Fact]
    public void Should_be_resolvable_for_all_modules()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNotificationElementInfrastructure()
            .AddNotificationElements<TestClientModuleA>()
            .AddNotificationElements<TestClientModuleB>();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var registries = serviceProvider.GetRequiredService<IEnumerable<INotificationElementRegistry>>().ToList();

        // Assert
        registries.Should().NotBeNull().And.HaveCount(2);
        registries.First().Should().BeAssignableTo<INotificationElementRegistry<TestClientModuleA>>();
        registries.Skip(1).First().Should().BeAssignableTo<INotificationElementRegistry<TestClientModuleB>>();
    }

    [Fact]
    public void Should_discover_notification_elements_on_instantiation()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNotificationElementInfrastructure()
            .AddNotificationElements<TestClientModuleA>();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var registry = serviceProvider.GetService<INotificationElementRegistry<TestClientModuleA>>();

        // Assert
        registry.Should().HaveCount(2);
        registry.Should().Contain(i => i.ComponentType == typeof(TestClientModuleA.NotificationElementA));
        registry.Should().Contain(i => i.ComponentType == typeof(TestClientModuleA.NotificationElementB));
    }

    [Fact]
    public void Should_have_registered_notification_element_parameters_of_first_discovered_notification_element()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNotificationElementInfrastructure()
            .AddNotificationElements<TestClientModuleA>();

        var serviceProvider = services.BuildServiceProvider();

        var registry = serviceProvider.GetRequiredService<INotificationElementRegistry<TestClientModuleA>>();

        // Act
        var result = registry.First(i => i.ComponentType == typeof(TestClientModuleA.NotificationElementA));

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(TestClientModuleA.NotificationElementA.DefaultId);
        result.ComponentType.Should().Be<TestClientModuleA.NotificationElementA>();
        result.State.Should().BeOfType<NotificationElementState>();
        result.State.IsActive.Should().Be(false);
    }

    [Fact]
    public void Should_have_registered_notification_element_parameters_after_add_operation()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNotificationElementInfrastructure()
            .AddNotificationElements<TestClientModuleA>();

        var serviceProvider = services.BuildServiceProvider();

        var registry = serviceProvider.GetRequiredService<INotificationElementRegistry<TestClientModuleA>>();

        var notificationElementId = Guid.NewGuid();
        var notificationElementState = new NotificationElementState();
        var authorizationRequirement = new AccessLevelAuthorizationRequirement(TestClientModuleA.ModuleId, AccessLevel.Partial);

        // Act
        var result = registry.Add<TestClientModuleA.NotificationElementA, NotificationElementState>(
            notificationElementState, id: notificationElementId, authorizationRequirement: authorizationRequirement);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(notificationElementId);
        result.ComponentType.Should().Be<TestClientModuleA.NotificationElementA>();
        result.State.Should().BeOfType<NotificationElementState>();
        result.State.IsActive.Should().Be(false);
        result.AuthorizationRequirement.Should().Be(authorizationRequirement);
    }

    [Fact]
    public void Should_trigger_changed_event_on_add_operation()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNotificationElementInfrastructure()
            .AddNotificationElements<TestClientModuleA>();

        var serviceProvider = services.BuildServiceProvider();

        var registryChanged = false;

        var registry = serviceProvider.GetRequiredService<INotificationElementRegistry<TestClientModuleA>>();
        registry.Changed += args => registryChanged = args.ItemsAdded.Count() == 1;

        var notificationElementId = Guid.NewGuid();
        var notificationElementState = new NotificationElementState();
        var authorizationRequirement = new AccessLevelAuthorizationRequirement(TestClientModuleA.ModuleId, AccessLevel.Partial);

        // Act
        registry.Add<TestClientModuleA.NotificationElementA, NotificationElementState>(
            notificationElementState, id: notificationElementId, authorizationRequirement: authorizationRequirement);

        // Assert
        registryChanged.Should().BeTrue();
    }

    [Fact]
    public void Should_trigger_changed_event_on_remove_by_id_operation()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNotificationElementInfrastructure()
            .AddNotificationElements<TestClientModuleA>();

        var serviceProvider = services.BuildServiceProvider();

        var registryChanged = false;

        var registry = serviceProvider.GetRequiredService<INotificationElementRegistry<TestClientModuleA>>();
        registry.Changed += args => registryChanged = true;

        // Act
        registry.Remove(registry.First().Id);

        // Assert
        registryChanged.Should().BeTrue();
    }

    [Fact]
    public void Should_trigger_changed_event_on_remove_by_item_operation()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNotificationElementInfrastructure()
            .AddNotificationElements<TestClientModuleA>();

        var serviceProvider = services.BuildServiceProvider();

        var registryChanged = false;

        var registry = serviceProvider.GetRequiredService<INotificationElementRegistry<TestClientModuleA>>();
        registry.Changed += args => registryChanged = true;

        // Act
        registry.Remove(registry.First());

        // Assert
        registryChanged.Should().BeTrue();
    }

    [Fact]
    public void Should_trigger_changed_event_on_remove_by_type_operation()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNotificationElementInfrastructure()
            .AddNotificationElements<TestClientModuleA>();

        var serviceProvider = services.BuildServiceProvider();

        var registryChanged = false;

        var registry = serviceProvider.GetRequiredService<INotificationElementRegistry<TestClientModuleA>>();
        registry.Changed += args => registryChanged = true;

        // Act
        registry.Remove<TestClientModuleA.NotificationElementA>();

        // Assert
        registryChanged.Should().BeTrue();
    }

    [Fact]
    public void Should_trigger_changed_event_on_remove_by_predicate_operation()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNotificationElementInfrastructure()
            .AddNotificationElements<TestClientModuleA>();

        var serviceProvider = services.BuildServiceProvider();

        var registryChanged = false;

        var registry = serviceProvider.GetRequiredService<INotificationElementRegistry<TestClientModuleA>>();
        registry.Changed += args => registryChanged = true;

        // Act
        var notificationElementsRemoved = registry.Remove(t => t.ComponentType.IsAssignableTo(typeof(INotificationElement)));

        // Assert
        registryChanged.Should().BeTrue();
        notificationElementsRemoved.Should().Be(2);
    }

    [Fact]
    public void Should_not_trigger_changed_event_after_begin_update()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNotificationElementInfrastructure()
            .AddNotificationElements<TestClientModuleA>();

        var serviceProvider = services.BuildServiceProvider();

        var registryChanged = false;

        var registry = serviceProvider.GetRequiredService<INotificationElementRegistry<TestClientModuleA>>();
        registry.Changed += args => registryChanged = true;

        // Act
        registry.BeginUpdate();

        while (registry.Any())
        {
            var registryItem = registry.First();

            registry.Remove(registryItem);
        }

        registry.Add<TestClientModuleA.NotificationElementA, NotificationElementState>(new NotificationElementState());

        // Assert
        registryChanged.Should().Be(false);
    }

    [Fact]
    public void Should_trigger_changed_event_on_end_update_with_correct_args()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNotificationElementInfrastructure()
            .AddNotificationElements<TestClientModuleA>();

        var serviceProvider = services.BuildServiceProvider();

        var registryChangedCounter = 0;
        var itemsRemoved = 0;
        var itemsAdded = 0;

        var registry = serviceProvider.GetRequiredService<INotificationElementRegistry<TestClientModuleA>>();
        registry.Changed += args =>
        {
            registryChangedCounter++;
            itemsRemoved = args.ItemsRemoved.Count();
            itemsAdded = args.ItemsAdded.Count();
        };

        // Act
        registry.BeginUpdate();
        try
        {
            var firstRegistryItem = registry.First();
            var secondRegistryItem = registry.Skip(1).First();

            registry.Remove(firstRegistryItem);
            registry.Remove(secondRegistryItem);

            registry.Add<TestClientModuleA.NotificationElementA, NotificationElementState>(new NotificationElementState(), id: Guid.NewGuid());
            registry.Add<TestClientModuleA.NotificationElementA, NotificationElementState>(new NotificationElementState());
            registry.Add<TestClientModuleA.NotificationElementB, NotificationElementState>(new NotificationElementState());
        }
        finally
        {
            registry.EndUpdate();
        }

        // Assert
        registryChangedCounter.Should().Be(1);
        itemsRemoved.Should().Be(2);
        itemsAdded.Should().Be(3);
    }

    [Fact]
    public async Task Should_trigger_changed_event_on_outer_end_update()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNotificationElementInfrastructure()
            .AddNotificationElements<TestClientModuleA>();

        var serviceProvider = services.BuildServiceProvider();

        var registryChangedCounter = 0;

        var registry = serviceProvider.GetRequiredService<INotificationElementRegistry<TestClientModuleA>>();
        registry.Changed += args => ++registryChangedCounter;

        static async Task RandomUpdateTask(Random random, INotificationElementRegistry<TestClientModuleA> registry)
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
                        registry.Remove(registryItem);
                    else
                        coinToss = 1;
                }

                if (coinToss == 1)
                    registry.Add<TestClientModuleA.NotificationElementA, NotificationElementState>(new NotificationElementState());
            }
            finally
            {
                registry.EndUpdate();
            }

            await Task.CompletedTask;
        }

        // Act
        registry.BeginUpdate();
        try
        {
            var random = new Random();

            var updateTasks = new List<Task>();
            for (var i = 0; i < 1000; i++)
                updateTasks.Add(RandomUpdateTask(random, registry));

            await Task.WhenAll(updateTasks);
        }
        finally
        {
            registry.EndUpdate();
        }

        // Assert
        registryChangedCounter.Should().Be(1);
    }

    [Fact]
    public void Should_increase_update_lock_on_begin_update()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNotificationElementInfrastructure()
            .AddNotificationElements<TestClientModuleA>();

        var serviceProvider = services.BuildServiceProvider();

        var registry = serviceProvider.GetRequiredService<INotificationElementRegistry<TestClientModuleA>>();

        // Act
        registry.BeginUpdate();

        // Assert
        registry.UpdateLock.Should().Be(1);
    }

    [Fact]
    public void Should_decrease_update_lock_on_end_update()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNotificationElementInfrastructure()
            .AddNotificationElements<TestClientModuleA>();

        var serviceProvider = services.BuildServiceProvider();

        var registry = serviceProvider.GetRequiredService<INotificationElementRegistry<TestClientModuleA>>();

        // Act
        registry.BeginUpdate();
        registry.EndUpdate();

        // Assert
        registry.UpdateLock.Should().Be(0);
    }

    public sealed class TestClientModuleA : IClientModule
    {
        public const string ModuleId = nameof(TestClientModuleA);

        public ModuleKey ModuleKey => new() { ModuleId = ModuleId };

        [InitialNotificationElement<TestClientModuleA>(Id = DefaultId)]
        [ModuleAuthorize(moduleId: ModuleId, accessLevel: AccessLevel.Partial)]
        public sealed class NotificationElementA : NotificationElementBase<NotificationElementState>
        {
            internal const string DefaultId = "59102a83-2a39-44f9-ab07-2cfdb256b2ed";
        }

        [InitialNotificationElement<TestClientModuleA>]
        public sealed class NotificationElementB : NotificationElementBase<NotificationElementState>;
    }

    public sealed class TestClientModuleB : IClientModule
    {
        public ModuleKey ModuleKey => new();
    }
}
