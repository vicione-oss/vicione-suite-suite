using Sdk.Client.NavTiles.Components;

namespace Blazor.Shared.Tests.NavTiles;

public sealed class NavTileStateTests
{
    [Fact]
    public void Should_indicate_loading_after_begin_loading()
    {
        // Arrange
        var navTileState = new NavTileState();

        // Act
        navTileState.BeginLoading();

        // Assert
        navTileState.IsLoading.Should().BeTrue();
    }

    [Fact]
    public void Should_not_indicate_loading_after_end_loading()
    {
        // Arrange
        var navTileState = new NavTileState();

        // Act
        navTileState.EndLoading();

        // Assert
        navTileState.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void Should_indicate_loading_after_incomplete_loading_call_sequence()
    {
        // Arrange
        var navTileState = new NavTileState();

        // Act
        navTileState.BeginLoading();
        navTileState.BeginLoading();
        navTileState.EndLoading();

        // Assert
        navTileState.IsLoading.Should().BeTrue();
    }

    [Fact]
    public void Should_not_indicate_loading_after_complete_loading_call_sequence()
    {
        // Arrange
        var navTileState = new NavTileState();

        // Act
        navTileState.BeginLoading();
        navTileState.BeginLoading();
        navTileState.EndLoading();
        navTileState.EndLoading();

        // Assert
        navTileState.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void Should_trigger_changed_event_on_begin_loading()
    {
        // Arrange
        var changedTriggered = false;

        var navTileState = new NavTileState();
        navTileState.Changed += () => changedTriggered = true;

        // Act
        navTileState.BeginLoading();

        // Assert
        changedTriggered.Should().Be(true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Should_handle_changed_event_on_end_loading(bool isLoading)
    {
        // Arrange
        var changedTriggered = false;

        var navTileState = new NavTileState();

        if (isLoading)
            navTileState.BeginLoading();

        navTileState.Changed += () => changedTriggered = true;

        // Act
        navTileState.EndLoading();

        // Assert
        changedTriggered.Should().Be(true);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void Should_handle_changed_event_when_enabled_is_set(bool isEnabledInitially, bool isEnabled, bool shouldTriggerChangedEvent)
    {
        // Arrange
        var changedTriggered = false;

        var navTileState = new NavTileState { Enabled = isEnabledInitially };
        navTileState.Changed += () => changedTriggered = true;

        // Act
        navTileState.Enabled = isEnabled;

        // Assert
        changedTriggered.Should().Be(shouldTriggerChangedEvent);
    }

    [Theory]
    [InlineData(null, "/foo", true)]
    [InlineData("/foo", "/foo", false)]
    [InlineData("/foo", "/bar", true)]
    [InlineData("/bar", null, true)]
    public void Should_handle_changed_event_when_link_target_is_set(string? initialLinkTarget, string? newLinkTarget, bool shouldTriggerChangedEvent)
    {
        // Arrange
        var changedTriggered = false;

        var navTileState = new NavTileState { LinkTarget = initialLinkTarget };
        navTileState.Changed += () => changedTriggered = true;

        // Act
        navTileState.LinkTarget = newLinkTarget;

        // Assert
        changedTriggered.Should().Be(shouldTriggerChangedEvent);
    }
}
