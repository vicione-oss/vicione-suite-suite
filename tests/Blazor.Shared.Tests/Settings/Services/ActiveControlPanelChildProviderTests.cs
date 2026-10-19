using Blazor.Shared.Settings.Services;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Tests.Settings.Services;

public sealed class ActiveControlPanelChildProviderTests
{
    private static IControlPanelRegistryItem CreateItem(IControlPanelParentDescriptor? childOf = null,
        IControlPanelParentDescriptor? asParent = null, int position = 0, bool hasContent = true)
    {
        var descriptor = Substitute.For<IControlPanelDescriptor>();
        descriptor.Position.Returns(position);
        descriptor.HasContent.Returns(hasContent);

        var item = Substitute.For<IControlPanelRegistryItem>();
        item.ChildOf.Returns(childOf);
        item.AsParent.Returns(asParent);
        item.Descriptor.Returns(descriptor);

        return item;
    }

    private static IControlPanelRegistry CreateRegistry(params IControlPanelRegistryItem[] items)
    {
        var registry = Substitute.For<IControlPanelRegistry>();
        registry.GetEnumerator().Returns(_ => ((IEnumerable<IControlPanelRegistryItem>)items).GetEnumerator());

        return registry;
    }

    [Fact]
    public void Should_return_null_when_parent_cannot_act_as_a_parent()
    {
        // Arrange - no AsParent descriptor means the item has no children to begin with.
        var parent = CreateItem();
        var unrelatedItem = CreateItem();

        var registries = new[] { CreateRegistry(parent, unrelatedItem) };
        var settingsModuleState = new SettingsModuleState();

        var provider = new ActiveControlPanelChildProvider(registries, settingsModuleState);

        // Act
        var activeChild = provider.GetActiveChild(parent);

        // Assert
        activeChild.Should().BeNull();
    }

    [Fact]
    public void Should_return_null_when_parent_has_no_children()
    {
        // Arrange
        var parentKey = Substitute.For<IControlPanelParentDescriptor>();
        var parent = CreateItem(asParent: parentKey);
        var unrelatedItem = CreateItem();

        var registries = new[] { CreateRegistry(parent, unrelatedItem) };
        var settingsModuleState = new SettingsModuleState();

        var provider = new ActiveControlPanelChildProvider(registries, settingsModuleState);

        // Act
        var activeChild = provider.GetActiveChild(parent);

        // Assert
        activeChild.Should().BeNull();
    }

    [Fact]
    public void Should_return_null_when_parent_has_children_but_none_is_explicitly_active()
    {
        // Arrange - null means the parent itself is the active "tab": every existing multi-tab control panel's
        // own inline content was always tab #1 before this mechanism existed, not an arbitrary first child.
        var parentKey = Substitute.For<IControlPanelParentDescriptor>();
        var parent = CreateItem(asParent: parentKey);
        var firstChild = CreateItem(childOf: parentKey, position: 1);
        var secondChild = CreateItem(childOf: parentKey, position: 2);

        var registries = new[] { CreateRegistry(parent, firstChild, secondChild) };
        var settingsModuleState = new SettingsModuleState();

        var provider = new ActiveControlPanelChildProvider(registries, settingsModuleState);

        // Act
        var activeChild = provider.GetActiveChild(parent);

        // Assert
        activeChild.Should().BeNull();
    }

    [Fact]
    public void Should_return_explicitly_set_active_child_when_it_is_still_a_child_of_parent()
    {
        // Arrange
        var parentKey = Substitute.For<IControlPanelParentDescriptor>();
        var parent = CreateItem(asParent: parentKey);
        var firstChild = CreateItem(childOf: parentKey, position: 1);
        var secondChild = CreateItem(childOf: parentKey, position: 2);

        var registries = new[] { CreateRegistry(parent, firstChild, secondChild) };
        var settingsModuleState = new SettingsModuleState();
        settingsModuleState.SetActiveChild(parent, secondChild);

        var provider = new ActiveControlPanelChildProvider(registries, settingsModuleState);

        // Act
        var activeChild = provider.GetActiveChild(parent);

        // Assert
        activeChild.Should().Be(secondChild);
    }

    [Fact]
    public void Should_return_null_when_explicitly_set_active_child_is_no_longer_a_child_of_parent()
    {
        // Arrange - falls back to the parent itself being active, not an arbitrary remaining child.
        var parentKey = Substitute.For<IControlPanelParentDescriptor>();
        var parent = CreateItem(asParent: parentKey);
        var firstChild = CreateItem(childOf: parentKey, position: 1);
        var staleChild = CreateItem();

        var registries = new[] { CreateRegistry(parent, firstChild) };
        var settingsModuleState = new SettingsModuleState();
        settingsModuleState.SetActiveChild(parent, staleChild);

        var provider = new ActiveControlPanelChildProvider(registries, settingsModuleState);

        // Act
        var activeChild = provider.GetActiveChild(parent);

        // Assert
        activeChild.Should().BeNull();
    }

    [Fact]
    public void Should_return_first_child_in_tab_order_when_parent_without_content_has_no_selected_child()
    {
        // Arrange
        var parentKey = Substitute.For<IControlPanelParentDescriptor>();
        var parent = CreateItem(asParent: parentKey, hasContent: false);
        var secondChild = CreateItem(childOf: parentKey, position: 2);
        var firstChild = CreateItem(childOf: parentKey, position: 1);

        var registries = new[] { CreateRegistry(parent, secondChild, firstChild) };
        var settingsModuleState = new SettingsModuleState();

        var provider = new ActiveControlPanelChildProvider(registries, settingsModuleState);

        // Act
        var activeChild = provider.GetActiveChild(parent);

        // Assert
        activeChild.Should().Be(firstChild);
    }

    [Fact]
    public void Should_return_explicitly_set_active_child_of_parent_without_content()
    {
        // Arrange
        var parentKey = Substitute.For<IControlPanelParentDescriptor>();
        var parent = CreateItem(asParent: parentKey, hasContent: false);
        var firstChild = CreateItem(childOf: parentKey, position: 1);
        var secondChild = CreateItem(childOf: parentKey, position: 2);

        var registries = new[] { CreateRegistry(parent, firstChild, secondChild) };
        var settingsModuleState = new SettingsModuleState();
        settingsModuleState.SetActiveChild(parent, secondChild);

        var provider = new ActiveControlPanelChildProvider(registries, settingsModuleState);

        // Act
        var activeChild = provider.GetActiveChild(parent);

        // Assert
        activeChild.Should().Be(secondChild);
    }
}
