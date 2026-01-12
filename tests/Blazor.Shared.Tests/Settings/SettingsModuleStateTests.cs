using System.Security.Cryptography;
using Blazor.Shared.Settings.Models;
using Blazor.Shared.Settings.Services;
using AwesomeAssertions;
using NSubstitute;
using Sdk.Client.ControlPanels.Services;
using Xunit;

namespace Blazor.Shared.Tests.Settings;

public sealed class SettingsModuleStateTests
{
    private readonly IHasChangeablePropertiesTests<SettingsModuleState> _tests = new();

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void Assert_changed_event_handling_when_show_navigate_back_button_is_set(bool initialValue, bool value, bool shouldTriggerChangedEvent)
        => _tests.AssertChangedEventHandlingWhenPropertyIsSet(state => state.ShowNavigateBackButton, initialValue, value, shouldTriggerChangedEvent);


    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void Assert_changed_event_handling_when_is_loading_overlay_visible_is_set(bool initialValue, bool value, bool shouldTriggerChangedEvent)
        => _tests.AssertChangedEventHandlingWhenPropertyIsSet(state => state.IsLoadingOverlayVisible, initialValue, value, shouldTriggerChangedEvent);

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void Assert_changed_event_handling_when_expanded_settings_category_is_set(bool hasInitialValue, bool hasValue, bool shouldTriggerChangedEvent)
    {
        var settingsCategory = new SettingsCategory { Title = "Foo" };

        _tests.AssertChangedEventHandlingWhenPropertyIsSet(state => state.ExpandedSettingsCategory,
            initialValue: hasInitialValue ? settingsCategory : null,
            value: hasValue ? settingsCategory : null,
            shouldTriggerChangedEvent);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void Assert_changed_event_handling_when_active_control_panel_registry_item_is_set(bool hasInitialValue, bool hasValue, bool shouldTriggerChangedEvent)
    {
        var activeControlPanelRegistryItem = Substitute.For<IControlPanelRegistryItem>();

        _tests.AssertChangedEventHandlingWhenPropertyIsSet(state => state.ActiveControlPanelRegistryItem,
            initialValue: hasInitialValue ? activeControlPanelRegistryItem : null,
            value: hasValue ? activeControlPanelRegistryItem : null,
            shouldTriggerChangedEvent);
    }

    [Theory]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, true)]
    public void Assert_changed_event_handling_when_last_active_control_panel_registry_item_map_is_changed(
        bool hasInitialEntries, bool addOrSetEntry, bool removeEntry, bool shouldTriggerChangedEvent)
    {
        // Arrange
        var settingsGroup = new SettingsGroup { Position = 1 };
        var settingsCategory = new SettingsCategory { Title = "Foo" };
        var settingsEntryKey = new SettingsEntriesKey { SettingsGroup = settingsGroup, SettingsCategory = settingsCategory };
        var controlPanelRegistryItem = Substitute.For<IControlPanelRegistryItem>();

        var state = new SettingsModuleState();

        if (hasInitialEntries)
            state.AddOrSetLastActiveControlPanelRegistryItem(settingsEntryKey, controlPanelRegistryItem);

        var changedTriggered = false;

        state.Changed += args => changedTriggered = args.PropertyNames.Contains(nameof(state.LastActiveControlPanelRegistryItemMap)); ;

        // Act
        if (addOrSetEntry)
            state.AddOrSetLastActiveControlPanelRegistryItem(settingsEntryKey, controlPanelRegistryItem);

        if (removeEntry)
            state.RemoveLastActiveControlPanelRegistryItem(settingsEntryKey);

        // Assert
        changedTriggered.Should().Be(shouldTriggerChangedEvent);
    }

    [Fact]
    public void Should_not_trigger_changed_event_after_begin_update()
    {
        // Arrange
        var state = new SettingsModuleState();

        var stateChanged = false;

        state.Changed += args => stateChanged = true;

        // Act
        state.BeginUpdate();

        state.ExpandedSettingsCategory = new SettingsCategory { Title = "Foo" };

        // Assert
        stateChanged.Should().Be(false);
    }

    [Fact]
    public void Should_trigger_changed_event_on_end_update_with_correct_args()
    {
        // Arrange
        var state = new SettingsModuleState();

        var changedCounter = 0;
        var affectedPropertyNames = new List<string>();

        state.Changed += args =>
        {
            changedCounter++;
            affectedPropertyNames.AddRange(args.PropertyNames);
        };

        // Act
        state.BeginUpdate();
        try
        {
            state.ExpandedSettingsCategory = new SettingsCategory { Title = "Foo" };
            state.ActiveControlPanelRegistryItem = Substitute.For<IControlPanelRegistryItem>();
        }
        finally
        {
            state.EndUpdate();
        }

        // Assert
        changedCounter.Should().Be(1);
        affectedPropertyNames.Should().HaveCount(2);
        affectedPropertyNames.Should().Contain(nameof(state.ExpandedSettingsCategory));
        affectedPropertyNames.Should().Contain(nameof(state.ActiveControlPanelRegistryItem));
    }

    [Fact]
    public async Task Should_trigger_changed_event_on_outer_end_update_async()
    {
        // Arrange
        var stateChangedCounter = 0;

        var state = new SettingsModuleState();
        state.Changed += args => ++stateChangedCounter;

        static async Task RandomUpdateTaskAsync(SettingsModuleState state)
        {
            var delay = RandomNumberGenerator.GetInt32(100);
            await Task.Delay(delay);

            state.BeginUpdate();
            try
            {
                var coinToss = RandomNumberGenerator.GetInt32(2);

                if (coinToss == 0)
                    state.ExpandedSettingsCategory = new SettingsCategory { Title = Guid.NewGuid().ToString() };
                else
                    state.ActiveControlPanelRegistryItem = Substitute.For<IControlPanelRegistryItem>();
            }
            finally
            {
                state.EndUpdate();
            }
        }

        // Act
        state.BeginUpdate();
        try
        {
            var updateTasks = new List<Task>();
            for (var i = 0; i < 1000; i++)
                updateTasks.Add(RandomUpdateTaskAsync(state));

            await Task.WhenAll(updateTasks);
        }
        finally
        {
            state.EndUpdate();
        }

        // Assert
        stateChangedCounter.Should().Be(1);
    }



    [Fact]
    public void Should_increase_update_lock_on_begin_update()
    {
        // Arrange
        var state = new SettingsModuleState();

        // Act
        state.BeginUpdate();

        // Assert
        state.UpdateLock.Should().Be(1);
    }

    [Fact]
    public void Should_decrease_update_lock_on_end_update()
    {
        // Arrange
        var state = new SettingsModuleState();

        // Act
        state.BeginUpdate();
        state.EndUpdate();

        // Assert
        state.UpdateLock.Should().Be(0);
    }
}
