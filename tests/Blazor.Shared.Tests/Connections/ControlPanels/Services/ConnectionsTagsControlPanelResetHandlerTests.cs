using Blazor.Shared.Connections.ControlPanels.Tags.Services;
using Blazor.Shared.Connections.Services;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Tests.Connections.ControlPanels.Services;

public sealed class ConnectionsTagsControlPanelResetHandlerTests
{
    [Fact]
    public async Task Should_load_the_tags_and_clear_pending_deletions()
    {
        // Arrange
        var tag = new Tag { Id = Guid.NewGuid(), Text = "Tag" };
        var connectionService = Substitute.For<ISuiteConnectionService>();
        connectionService.GetTags(Arg.Any<CancellationToken>()).Returns([tag]);

        var state = new ConnectionsTagsControlPanelState();
        state.DeletingTags.Add(new Tag { Id = Guid.NewGuid(), Text = "Pending" });

        var resetHandler = new ConnectionsTagsControlPanelResetHandler(connectionService);

        // Act
        await resetHandler.Reset(state, Xunit.TestContext.Current.CancellationToken);

        // Assert
        state.Tags.Keys.Should().BeEquivalentTo([tag.Id]);
        state.DeletingTags.Should().BeEmpty();
    }
}
