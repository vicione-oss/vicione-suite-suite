using Blazor.Shared.Connections.ControlPanels.Connections.Extensions;
using Blazor.Shared.Connections.ControlPanels.Connections.Services;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using Sdk.Messaging;

namespace Blazor.Shared.Tests.Connections.ControlPanels.Extensions;

public sealed class ConnectionsControlPanelStateExtensionsTests
{
    public sealed class UpdateTags
    {
        [Fact]
        public void Should_add_a_created_tag()
        {
            // Arrange
            var state = new ConnectionsControlPanelState();
            var tag = new Tag { Id = Guid.NewGuid(), Text = "Line 1" };
            var created = new TagsChanged(CrudAction.Created, [tag]) { CorrelationId = Guid.NewGuid() };

            // Act
            state.UpdateTags(created);

            // Assert
            state.Tags.Should().ContainKey(tag.Id).WhoseValue.Text.Should().Be("Line 1");
        }

        [Fact]
        public void Should_not_add_a_tag_whose_creation_failed()
        {
            // Arrange
            var state = new ConnectionsControlPanelState();
            var tag = new Tag { Id = Guid.NewGuid(), Text = "Line 1" };
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
            var tagId = Guid.NewGuid();
            var state = new ConnectionsControlPanelState { Tags = new() { [tagId] = new Tag { Id = tagId, Text = "Line 1" } } };
            var failedUpdate = new TagsChanged(CrudAction.Updated, [new Tag { Id = tagId, Text = "Line 2" }])
            {
                CorrelationId = Guid.NewGuid(),
                ErrorInfo = new ErrorInfo(500, "Update rejected")
            };

            // Act
            state.UpdateTags(failedUpdate);

            // Assert
            state.Tags[tagId].Text.Should().Be("Line 1");
        }
    }
}
