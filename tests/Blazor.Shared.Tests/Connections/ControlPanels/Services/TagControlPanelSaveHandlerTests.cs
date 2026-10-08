using Blazor.Shared.Connections.Contracts;
using Blazor.Shared.Connections.ControlPanels.Tags.Services;
using Blazor.Shared.Connections.Services;
using Sdk.Client.ControlPanels.Models;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Tests.Connections.ControlPanels.Services;

public sealed class TagControlPanelSaveHandlerTests
{
    private readonly ISuiteConnectionService _connectionService = Substitute.For<ISuiteConnectionService>();

    [Fact]
    public async Task Should_switch_to_edit_mode_when_creating_the_tag_succeeds()
    {
        // Arrange
        var state = new TagControlPanelState { Tag = new EditTagModel { Text = "Line 1" } };
        _connectionService.UpsertTag(Arg.Any<Tag>(), Arg.Any<CancellationToken>())
            .Returns(new SuiteConnectionServiceSuccessResult());

        var saveHandler = new TagControlPanelSaveHandler(_connectionService);

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        state.TagId.Should().Be(state.Tag.Id);
        state.IsEditMode.Should().BeTrue();
    }

    [Fact]
    public async Task Should_stay_in_create_mode_when_creating_the_tag_fails()
    {
        // Arrange
        var state = new TagControlPanelState { Tag = new EditTagModel { Text = "Line 1" } };
        _connectionService.UpsertTag(Arg.Any<Tag>(), Arg.Any<CancellationToken>())
            .Returns(new SuiteConnectionServiceErrorResult("Something went wrong"));

        var saveHandler = new TagControlPanelSaveHandler(_connectionService);

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>()
            .Which.Message.Should().Be("Something went wrong");
        state.TagId.Should().BeNull();
        state.IsEditMode.Should().BeFalse();
    }
}
