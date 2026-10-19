using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Settings.Factories;
using Blazor.Shared.Settings.Models;
using Blazor.Shared.Settings.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Tests.Settings.Models;

public sealed class ControlPanelEditScopeTests : IDisposable
{
    private sealed class TestControlPanel : ControlPanelBase<ControlPanelState>;

    private readonly ServiceProvider _serviceProvider = new ServiceCollection()
        .AddLogging()
        .AddControlPanelEditRegistry()
        .BuildServiceProvider();

    private IControlPanelEditRegistry ControlPanelEditRegistry => _serviceProvider.GetRequiredService<IControlPanelEditRegistry>();

    public void Dispose()
        => _serviceProvider.Dispose();

    [Fact]
    public async Task Should_return_the_same_edit_for_the_same_control_panel()
    {
        // Arrange
        await using var editScope = CreateEditScope();
        var controlPanelRegistryItem = CreateControlPanelRegistryItem();

        // Act
        editScope.TryGetOrCreateEdit(controlPanelRegistryItem, out var firstEdit);
        editScope.TryGetOrCreateEdit(controlPanelRegistryItem, out var secondEdit);

        // Assert
        secondEdit.Should().BeSameAs(firstEdit);
        ControlPanelEditRegistry.Should().BeEquivalentTo([firstEdit]);
    }

    [Fact]
    public async Task Should_return_different_edits_for_control_panels_sharing_a_state()
    {
        // Arrange
        await using var editScope = CreateEditScope();
        var controlPanelState = new ControlPanelState();

        // Act
        editScope.TryGetOrCreateEdit(CreateControlPanelRegistryItem(controlPanelState), out var firstEdit);
        editScope.TryGetOrCreateEdit(CreateControlPanelRegistryItem(controlPanelState), out var secondEdit);

        // Assert
        secondEdit.Should().NotBeSameAs(firstEdit);
        ControlPanelEditRegistry.Should().HaveCount(2);
    }

    [Fact]
    public async Task Should_mark_an_edit_as_reset_only_once_until_resets_are_cleared()
    {
        // Arrange
        await using var editScope = CreateEditScope();
        var controlPanelEdit = Substitute.For<IControlPanelEdit>();

        // Act
        var firstMark = editScope.TryMarkReset(controlPanelEdit);
        var secondMark = editScope.TryMarkReset(controlPanelEdit);
        editScope.ClearResets();
        var markAfterClear = editScope.TryMarkReset(controlPanelEdit);

        // Assert
        firstMark.Should().BeTrue();
        secondMark.Should().BeFalse();
        markAfterClear.Should().BeTrue();
    }

    [Fact]
    public async Task Should_mark_edits_without_a_shared_reset_handler_as_reset_independently()
    {
        // Arrange
        await using var editScope = CreateEditScope();

        // Act
        var firstMark = editScope.TryMarkReset(Substitute.For<IControlPanelEdit>());
        var secondMark = editScope.TryMarkReset(Substitute.For<IControlPanelEdit>());

        // Assert
        firstMark.Should().BeTrue();
        secondMark.Should().BeTrue();
    }

    [Fact]
    public async Task Should_mark_edits_sharing_a_reset_handler_as_reset_once()
    {
        // Arrange
        await using var editScope = CreateEditScope();

        var parentEdit = Substitute.For<IControlPanelEdit>();
        parentEdit.SharedResetGroup.Returns(new SharedResetGroup(typeof(TestControlPanel), typeof(IControlPanelResetHandler<ControlPanelState>)));

        var tabEdit = Substitute.For<IControlPanelEdit>();
        tabEdit.SharedResetGroup.Returns(new SharedResetGroup(typeof(TestControlPanel), typeof(IControlPanelResetHandler<ControlPanelState>)));

        // Act
        var parentMark = editScope.TryMarkReset(parentEdit);
        var tabMark = editScope.TryMarkReset(tabEdit);

        // Assert
        parentMark.Should().BeTrue();
        tabMark.Should().BeFalse();
    }

    [Fact]
    public async Task Should_mark_edits_sharing_a_reset_handler_type_with_different_owners_as_reset_independently()
    {
        // Arrange
        await using var editScope = CreateEditScope();

        var firstEdit = Substitute.For<IControlPanelEdit>();
        firstEdit.SharedResetGroup.Returns(new SharedResetGroup(typeof(TestControlPanel), typeof(IControlPanelResetHandler<ControlPanelState>)));

        var secondEdit = Substitute.For<IControlPanelEdit>();
        secondEdit.SharedResetGroup.Returns(new SharedResetGroup(typeof(ControlPanelState), typeof(IControlPanelResetHandler<ControlPanelState>)));

        // Act
        var firstMark = editScope.TryMarkReset(firstEdit);
        var secondMark = editScope.TryMarkReset(secondEdit);

        // Assert
        firstMark.Should().BeTrue();
        secondMark.Should().BeTrue();
    }

    [Fact]
    public async Task Should_remove_its_edits_from_the_registry_when_disposed()
    {
        // Arrange
        var otherEdit = Substitute.For<IControlPanelEdit>();
        ControlPanelEditRegistry.Add(otherEdit);

        var editScope = CreateEditScope();
        editScope.TryGetOrCreateEdit(CreateControlPanelRegistryItem(), out _);

        // Act
        await editScope.DisposeAsync();
        await editScope.DisposeAsync();

        // Assert
        ControlPanelEditRegistry.Should().BeEquivalentTo([otherEdit]);
    }

    [Fact]
    public async Task Should_refuse_new_edits_when_disposed()
    {
        // Arrange
        var editScope = CreateEditScope();
        await editScope.DisposeAsync();

        // Act
        var created = editScope.TryGetOrCreateEdit(CreateControlPanelRegistryItem(), out var controlPanelEdit);

        // Assert
        created.Should().BeFalse();
        controlPanelEdit.Should().BeNull();
        ControlPanelEditRegistry.Should().BeEmpty();
    }

    private ControlPanelEditScope CreateEditScope()
        => new(ControlPanelEditRegistry, new ControlPanelEditFactory(_serviceProvider));

    private static IControlPanelRegistryItem CreateControlPanelRegistryItem(ControlPanelState? controlPanelState = null)
    {
        var controlPanelRegistryItem = Substitute.For<IControlPanelRegistryItem>();
        controlPanelRegistryItem.ComponentType.Returns(typeof(TestControlPanel));
        controlPanelRegistryItem.State.Returns(controlPanelState ?? new ControlPanelState());

        return controlPanelRegistryItem;
    }
}
