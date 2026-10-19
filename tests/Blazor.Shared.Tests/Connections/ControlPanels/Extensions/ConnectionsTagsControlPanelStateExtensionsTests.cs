using Blazor.Shared.Connections.ControlPanels.Tags.Extensions;
using Blazor.Shared.Connections.ControlPanels.Tags.Services;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using Sdk.Messaging;

namespace Blazor.Shared.Tests.Connections.ControlPanels.Extensions;

public sealed class ConnectionsTagsControlPanelStateExtensionsTests
{
    private static Tag CreateTag(string text)
        => new() { Id = Guid.NewGuid(), Text = text };

    private static TagsChanged CreateTagsChanged(CrudAction action, params Tag[] tags)
        => new(action, [.. tags]) { CorrelationId = Guid.NewGuid() };

    [Fact]
    public void Should_add_a_created_tag()
    {
        // Arrange
        var existingTag = CreateTag("Existing");
        var createdTag = CreateTag("Created");
        var state = new ConnectionsTagsControlPanelState { Tags = new() { [existingTag.Id] = existingTag } };

        // Act
        state.UpdateTags(CreateTagsChanged(CrudAction.Created, createdTag));

        // Assert
        state.Tags.Keys.Should().BeEquivalentTo([existingTag.Id, createdTag.Id]);
    }

    [Fact]
    public void Should_replace_an_updated_tag()
    {
        // Arrange
        var tag = CreateTag("Before");
        var updatedTag = new Tag { Id = tag.Id, Text = "After" };
        var state = new ConnectionsTagsControlPanelState { Tags = new() { [tag.Id] = tag } };

        // Act
        state.UpdateTags(CreateTagsChanged(CrudAction.Updated, updatedTag));

        // Assert
        state.Tags[tag.Id].Text.Should().Be("After");
    }

    [Fact]
    public void Should_remove_a_deleted_tag()
    {
        // Arrange
        var keptTag = CreateTag("Kept");
        var deletedTag = CreateTag("Deleted");
        var state = new ConnectionsTagsControlPanelState
        {
            Tags = new() { [keptTag.Id] = keptTag, [deletedTag.Id] = deletedTag }
        };

        // Act
        state.UpdateTags(CreateTagsChanged(CrudAction.Deleted, deletedTag));

        // Assert
        state.Tags.Keys.Should().BeEquivalentTo([keptTag.Id]);
    }

    [Fact]
    public void Should_not_add_a_tag_whose_creation_failed()
    {
        // Arrange
        var tag = CreateTag("Line 1");
        var state = new ConnectionsTagsControlPanelState();
        var failedCreate = new TagsChanged(CrudAction.Created, [tag])
        {
            CorrelationId = Guid.NewGuid(),
            ErrorInfo = new ErrorInfo(409, "Tag already exists")
        };

        // Act
        state.UpdateTags(failedCreate);

        // Assert
        state.Tags.Should().NotContainKey(tag.Id);
    }

    [Fact]
    public void Should_keep_the_stored_text_when_an_update_failed()
    {
        // Arrange
        var tag = CreateTag("Line 1");
        var state = new ConnectionsTagsControlPanelState { Tags = new() { [tag.Id] = tag } };
        var failedUpdate = new TagsChanged(CrudAction.Updated, [new Tag { Id = tag.Id, Text = "Line 2" }])
        {
            CorrelationId = Guid.NewGuid(),
            ErrorInfo = new ErrorInfo(500, "Update rejected")
        };

        // Act
        state.UpdateTags(failedUpdate);

        // Assert
        state.Tags[tag.Id].Text.Should().Be("Line 1");
    }

    [Fact]
    public void Should_keep_a_tag_whose_deletion_failed()
    {
        // Arrange
        var tag = CreateTag("Line 1");
        var state = new ConnectionsTagsControlPanelState { Tags = new() { [tag.Id] = tag } };
        var failedDelete = new TagsChanged(CrudAction.Deleted, [new Tag { Id = tag.Id }])
        {
            CorrelationId = Guid.NewGuid(),
            ErrorInfo = new ErrorInfo(500, "Delete rejected")
        };

        // Act
        state.UpdateTags(failedDelete);

        // Assert
        state.Tags.Should().ContainKey(tag.Id);
    }

    [Theory]
    [InlineData(CrudAction.Created)]
    [InlineData(CrudAction.Updated)]
    public void Should_not_list_a_tag_that_is_marked_for_deletion(CrudAction action)
    {
        // Arrange
        var tag = CreateTag("Line 1");
        var state = new ConnectionsTagsControlPanelState();
        state.DeletingTags.Add(tag);

        // Act
        state.UpdateTags(CreateTagsChanged(action, new Tag { Id = tag.Id, Text = "Line 2" }));

        // Assert
        state.Tags.Should().NotContainKey(tag.Id);
    }
}
