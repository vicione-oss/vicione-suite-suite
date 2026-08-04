using Blazor.Shared.Profile.ControlPanels.Passkeys.Services;
using Sdk.Client.Infrastructure;
using PasskeyConstants = Core.Shared.Passkeys.Constants;

namespace Blazor.Shared.Tests.Settings.Profile.ControlPanels.Passkeys;

public class EditPasskeyControlPanelSaveHandlerTest
{
    [Fact]
    public async Task Should_return_error_when_user_id_is_null()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        var result = await sut.Save(new EditPasskeyControlPanelState
            {
                UserId = null,
                PasskeyId = "some-id",
                NewName = "new name"
            },
            TestContext.Current.CancellationToken);

        // Assert
        result.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Should_return_error_when_new_name_is_empty()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        var result = await sut.Save(new EditPasskeyControlPanelState
            {
                UserId = "user-id",
                PasskeyId = "some-id",
                NewName = string.Empty
            },
            TestContext.Current.CancellationToken);

        // Assert
        result.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Should_return_error_when_new_name_is_already_taken_by_another_passkey()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        var result = await sut.Save(new EditPasskeyControlPanelState
            {
                UserId = "user-id",
                PasskeyId = "some-id",
                CurrentName = "my phone",
                NewName = "MyTablet",
                ExistingNames = ["my phone", "MyTablet"]
            },
            TestContext.Current.CancellationToken);

        // Assert
        result.Message.Should().NotBeNullOrEmpty()
            .And.ContainEquivalentOf("name");
    }

    [Fact]
    public async Task Should_allow_saving_the_same_name_as_current()
    {
        // Arrange
        using var sut = new EditPasskeyControlPanelSaveHandler(Substitute.For<IUiMediator>());
        var state = new EditPasskeyControlPanelState
        {
            UserId = "user-id",
            PasskeyId = "some-id",
            CurrentName = "my phone",
            NewName = "my phone",
            ExistingNames = ["my phone", "my tablet"]
        };

        // Act
        var result = await sut.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Message.Should().NotContainEquivalentOf("name");
    }

    [Fact]
    public async Task Should_return_error_when_new_name_exceeds_max_length()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        var result = await sut.Save(new EditPasskeyControlPanelState
            {
                UserId = "user-id",
                PasskeyId = "some-id",
                NewName = new string('a', PasskeyConstants.MaxPasskeyNameLength + 1)
            },
            TestContext.Current.CancellationToken);

        // Assert
        result.Message.Should().NotBeNullOrEmpty()
            .And.ContainEquivalentOf("name");
    }

    private static EditPasskeyControlPanelSaveHandler CreateSut()
        => new(Substitute.For<IUiMediator>());
}
