using AwesomeAssertions;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Settings.Models;
using Blazor.Shared.Settings.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Blazor.Shared.Tests.Settings.Services;

public sealed class ControlPanelEditRegistryTests
{
    [Fact]
    public void Should_be_resolvable_per_module()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddControlPanelEditRegistry();

        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var controlPanelEditRegistry = serviceProvider.GetService<IControlPanelEditRegistry>();

        // Assert
        controlPanelEditRegistry.Should().NotBeNull();
    }

    [Fact]
    public void Assert_registered_items_after_add_operation()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddControlPanelEditRegistry();

        using var serviceProvider = services.BuildServiceProvider();

        var controlPanelEditRegistry = serviceProvider.GetRequiredService<IControlPanelEditRegistry>();
        var controlPanelEdit = Substitute.For<IControlPanelEdit>();

        // Act
        controlPanelEditRegistry.Add(controlPanelEdit);

        // Assert
        controlPanelEditRegistry.Should().BeEquivalentTo([controlPanelEdit]);
    }

    [Fact]
    public void Should_trigger_changed_event_on_add_operation()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddControlPanelEditRegistry();

        using var serviceProvider = services.BuildServiceProvider();

        var controlPanelEditRegistryChanged = false;

        var controlPanelEditRegistry = serviceProvider.GetRequiredService<IControlPanelEditRegistry>();
        controlPanelEditRegistry.Changed += args => controlPanelEditRegistryChanged = true;

        var controlPanelEdit = Substitute.For<IControlPanelEdit>();

        // Act
        controlPanelEditRegistry.Add(controlPanelEdit);

        // Assert
        controlPanelEditRegistryChanged.Should().BeTrue();
    }

    [Fact]
    public void Should_trigger_changed_event_on_remove_by_item_operation()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddControlPanelEditRegistry();

        using var serviceProvider = services.BuildServiceProvider();

        var controlPanelEditRegistryChanged = false;

        var controlPanelEditRegistry = serviceProvider.GetRequiredService<IControlPanelEditRegistry>();

        var controlPanelEdit = Substitute.For<IControlPanelEdit>();
        controlPanelEditRegistry.Add(controlPanelEdit);

        controlPanelEditRegistry.Changed += args => controlPanelEditRegistryChanged = true;

        // Act
        var controlPanelEditRemoved = controlPanelEditRegistry.Remove(controlPanelEdit);

        // Assert
        controlPanelEditRegistryChanged.Should().BeTrue();
        controlPanelEditRemoved.Should().Be(true);
    }

    [Fact]
    public void Should_trigger_changed_event_on_remove_by_predicate_operation()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddControlPanelEditRegistry();

        using var serviceProvider = services.BuildServiceProvider();

        var controlPanelEditRegistryChanged = false;

        var controlPanelEditRegistry = serviceProvider.GetRequiredService<IControlPanelEditRegistry>();

        var controlPanelEdit1 = Substitute.For<IControlPanelEdit>();
        controlPanelEditRegistry.Add(controlPanelEdit1);

        var controlPanelEdit2 = Substitute.For<IControlPanelEdit>();
        controlPanelEditRegistry.Add(controlPanelEdit2);

        controlPanelEditRegistry.Changed += args => controlPanelEditRegistryChanged = true;

        // Act
        var controlPanelEditsRemoved = controlPanelEditRegistry.Remove(controlPanelEdit => controlPanelEdit == controlPanelEdit2);

        // Assert
        controlPanelEditRegistryChanged.Should().BeTrue();
        controlPanelEditsRemoved.Should().Be(1);
    }

    [Fact]
    public void Should_not_trigger_changed_event_after_begin_update()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddControlPanelEditRegistry();

        using var serviceProvider = services.BuildServiceProvider();

        var controlPanelEditRegistry = serviceProvider.GetRequiredService<IControlPanelEditRegistry>();

        var controlPanelEdit1 = Substitute.For<IControlPanelEdit>();
        var controlPanelEdit2 = Substitute.For<IControlPanelEdit>();

        var controlPanelEditRegistryChanged = false;
        controlPanelEditRegistry.Changed += args => controlPanelEditRegistryChanged = true;

        // Act
        controlPanelEditRegistry.BeginUpdate();

        controlPanelEditRegistry.Add(controlPanelEdit1);
        controlPanelEditRegistry.Add(controlPanelEdit2);

        while (controlPanelEditRegistry.Any())
            controlPanelEditRegistry.Remove(controlPanelEditRegistry.First());

        // Assert
        controlPanelEditRegistryChanged.Should().Be(false);
    }

    [Fact]
    public void Should_trigger_changed_event_on_end_update_with_correct_args()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddControlPanelEditRegistry();

        using var serviceProvider = services.BuildServiceProvider();

        var controlPanelEdit1 = Substitute.For<IControlPanelEdit>();
        var controlPanelEdit2 = Substitute.For<IControlPanelEdit>();

        var registryChangedCounter = 0;
        var itemsRemoved = 0;
        var itemsAdded = 0;

        var controlPanelEditRegistry = serviceProvider.GetRequiredService<IControlPanelEditRegistry>();
        controlPanelEditRegistry.Add(controlPanelEdit1);
        controlPanelEditRegistry.Add(controlPanelEdit2);

        controlPanelEditRegistry.Changed += args =>
        {
            registryChangedCounter++;
            itemsRemoved = args.ItemsRemoved.Count();
            itemsAdded = args.ItemsAdded.Count();
        };

        // Act
        controlPanelEditRegistry.BeginUpdate();
        try
        {
            var firstRegistryItem = controlPanelEditRegistry.First();
            var secondRegistryItem = controlPanelEditRegistry.Skip(1).First();

            controlPanelEditRegistry.Remove(firstRegistryItem);
            controlPanelEditRegistry.Remove(secondRegistryItem);

            controlPanelEditRegistry.Add(Substitute.For<IControlPanelEdit>());
        }
        finally
        {
            controlPanelEditRegistry.EndUpdate();
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
        var services = new ServiceCollection()
            .AddControlPanelEditRegistry();

        await using var serviceProvider = services.BuildServiceProvider();

        var controlPanelRegistryEditChangedCounter = 0;

        var controlPanelEditRegistry = serviceProvider.GetRequiredService<IControlPanelEditRegistry>();
        controlPanelEditRegistry.Changed += args => ++controlPanelRegistryEditChangedCounter;

        static async Task RandomUpdateTask(Random random, IControlPanelEditRegistry registry)
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
                {
                    registry.Add(Substitute.For<IControlPanelEdit>());
                }
            }
            finally
            {
                registry.EndUpdate();
            }
        }

        // Act
        controlPanelEditRegistry.BeginUpdate();
        try
        {
            var random = new Random();

            var updateTasks = new List<Task>();
            for (var i = 0; i < 1000; i++)
                updateTasks.Add(RandomUpdateTask(random, controlPanelEditRegistry));

            await Task.WhenAll(updateTasks);
        }
        finally
        {
            controlPanelEditRegistry.EndUpdate();
        }

        // Assert
        controlPanelRegistryEditChangedCounter.Should().Be(1);
    }

    [Fact]
    public void Should_increase_update_lock_on_begin_update()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddControlPanelEditRegistry();

        using var serviceProvider = services.BuildServiceProvider();

        var controlPanelEditRegistry = serviceProvider.GetRequiredService<IControlPanelEditRegistry>();

        // Act
        controlPanelEditRegistry.BeginUpdate();

        // Assert
        controlPanelEditRegistry.UpdateLock.Should().Be(1);
    }

    [Fact]
    public void Should_decrease_update_lock_on_end_update()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddControlPanelEditRegistry();

        using var serviceProvider = services.BuildServiceProvider();

        var controlPanelEditRegistry = serviceProvider.GetRequiredService<IControlPanelEditRegistry>();

        // Act
        controlPanelEditRegistry.BeginUpdate();
        controlPanelEditRegistry.EndUpdate();

        // Assert
        controlPanelEditRegistry.UpdateLock.Should().Be(0);
    }
}
