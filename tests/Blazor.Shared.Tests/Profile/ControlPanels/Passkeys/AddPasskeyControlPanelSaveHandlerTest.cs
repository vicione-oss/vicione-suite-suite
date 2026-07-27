using AwesomeAssertions;
using Core.Shared.Passkeys.Contracts;
using Blazor.Shared.Profile.ControlPanels.Passkeys.Services;
using Blazor.Shared.Settings.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using NSubstitute;
using Xunit;
using PasskeyConstants = Core.Shared.Passkeys.Constants;

namespace Blazor.Shared.Tests.Settings.Profile.ControlPanels.Passkeys;

public sealed class AddPasskeyControlPanelSaveHandlerTest
{
    [Fact]
    public async Task Should_validate_that_name_is_not_null()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Save(new AddPasskeyControlPanelState()
            {
                Name = string.Empty,
            },
            TestContext.Current.CancellationToken);

        // Assert
        result.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Should_validate_that_name_is_unique()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Save(new AddPasskeyControlPanelState()
            {
                ExistingUserPasskeys =
                [
                    new PasskeyInfo("id-1", "test")
                ],
                Name = "TesT",
            },
            TestContext.Current.CancellationToken);

        // Assert
        result.Message.Should().NotBeNullOrEmpty()
            .And.ContainEquivalentOf("name");
    }

    [Fact]
    public async Task Should_validate_that_name_does_not_exceed_max_length()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Save(new AddPasskeyControlPanelState()
            {
                Name = new string('a', PasskeyConstants.MaxPasskeyNameLength + 1),
            },
            TestContext.Current.CancellationToken);

        // Assert
        result.Message.Should().NotBeNullOrEmpty()
            .And.ContainEquivalentOf("name");
    }

    [Fact]
    public async Task Should_request_navigate_back_after_successful_save()
    {
        // Arrange
        var sut = CreateSut(Options.Create(new AntiforgeryOptions()));

        // Act
        var result = await sut.Save(new AddPasskeyControlPanelState()
            {
                Name = "test",
            },
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<NavigateBackOnSaveSuccessResult>();
    }

    private static AddPasskeyControlPanelSaveHandler CreateSut(IOptions<AntiforgeryOptions>? antiforgeryOptions = null)
        => new(
            Substitute.For<AntiforgeryStateProvider>(),
            antiforgeryOptions ?? Substitute.For<IOptions<AntiforgeryOptions>>(),
            Substitute.For<IJSRuntime>(),
            Substitute.For<ILogger<AddPasskeyControlPanelSaveHandler>>()
        );
}
