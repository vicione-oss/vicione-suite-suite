using AwesomeAssertions;
using Blazor.Shared.Dialogs;
using Bunit;
using Bunit.Rendering;
using ViciOne.Ui.Blazor.Components.Dialog.Components;
using ViciOne.Ui.Blazor.Components.Dialog.Extensions;
using ViciOne.Ui.Blazor.Components.Popup.Components;
using ViciOne.Ui.Blazor.Components.TestingHelpers.Dialog.Extensions;
using Xunit;

namespace Blazor.Shared.Tests.Dialogs;

public sealed class ConfirmCancelDialogTests
{
    [Fact]
    public async Task Should_render_component()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();

        // Act
        var cut = ctx.Render<ConfirmCancelDialog>();

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_render_hidden()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();

        // Act
        var cut = ctx.Render<ConfirmCancelDialog>();

        // Assert
        var innerDialog = cut.FindComponent<Dialog>();

        Assert.Throws<ComponentNotFoundException>(() => innerDialog.RenderSectionContent(ctx));
    }

    [Fact]
    public async Task Should_render_visible()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();

        // Act
        var cut = ctx.Render<ConfirmCancelDialog>(parameters => parameters
            .Add(p => p.Visible, true));

        var innerDialog = cut.FindComponent<Dialog>();
        var sectionContent = innerDialog.RenderSectionContent(ctx);

        // Assert
        sectionContent.Find(".confirm-cancel-dialog").Should().NotBeNull();
    }

    [Fact]
    public async Task Should_fire_on_confirm_event_on_confirm_button_click()
    {
        // Arrange
        var onConfirmFired = false;
        var onCancelFired = false;
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();

        // Act
        var cut = ctx.Render<ConfirmCancelDialog>(parameters => parameters
            .Add(p => p.OnConfirm, () => { onConfirmFired = true; })
            .Add(p => p.OnCancel, () => { onCancelFired = true; })
            .Add(p => p.Visible, true));

        var innerDialog = cut.FindComponent<Dialog>();
        var sectionContent = innerDialog.RenderSectionContent(ctx);
        var confirmButton = sectionContent.FindFooterButton(button => button.Text == "Confirm");
        var innerButton = confirmButton.FindInnerButton();

        await innerButton.ClickAsync();

        // Assert
        onConfirmFired.Should().BeTrue();
        onCancelFired.Should().BeFalse();
    }

    [Fact]
    public async Task Should_fire_on_cancel_event_on_cancel_button_click()
    {
        // Arrange
        var onConfirmFired = false;
        var onCancelFired = false;
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();

        // Act
        var cut = ctx.Render<ConfirmCancelDialog>(parameters => parameters
            .Add(p => p.OnConfirm, () => { onConfirmFired = true; })
            .Add(p => p.OnCancel, () => { onCancelFired = true; })
            .Add(p => p.Visible, true));

        var innerDialog = cut.FindComponent<Dialog>();
        var sectionContent = innerDialog.RenderSectionContent(ctx);
        var confirmButton = sectionContent.FindFooterButton(button => button.Text == "Cancel");
        var innerButton = confirmButton.FindInnerButton();

        await innerButton.ClickAsync();

        // Assert
        onConfirmFired.Should().BeFalse();
        onCancelFired.Should().BeTrue();
    }

    [Fact]
    public async Task Should_fire_on_cancel_event_on_close_button_click()
    {
        // Arrange
        var onConfirmFired = false;
        var onCancelFired = false;
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();

        // Act
        var cut = ctx.Render<ConfirmCancelDialog>(parameters => parameters
            .Add(p => p.OnConfirm, () => { onConfirmFired = true; })
            .Add(p => p.OnCancel, () => { onCancelFired = true; })
            .Add(p => p.Visible, true));

        var innerDialog = cut.FindComponent<Dialog>();
        var sectionContent = innerDialog.RenderSectionContent(ctx);
        var closeButton = sectionContent.FindComponent<PopupHeaderCloseActionButton>();
        var innerButton = closeButton.Find("button");

        await innerButton.ClickAsync();

        // Assert
        onConfirmFired.Should().BeFalse();
        onCancelFired.Should().BeTrue();
    }

    [Fact]
    public async Task Should_pass_header_text_to_dialog()
    {
        // Arrange
        var headerText = "Test header";
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();

        // Act
        var cut = ctx.Render<ConfirmCancelDialog>(parameters => parameters
            .Add(p => p.HeaderText, headerText)
            .Add(p => p.Visible, true));

        var innerDialog = cut.FindComponent<Dialog>();

        // Assert
        innerDialog.Instance.HeaderText.Should().Be(headerText);
    }

    [Fact]
    public async Task Should_render_body()
    {
        // Arrange
        var body = "<p>Test body</p>";
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();

        // Act
        var cut = ctx.Render<ConfirmCancelDialog>(parameters => parameters
            .Add(p => p.Body, body)
            .Add(p => p.Visible, true));

        var innerDialog = cut.FindComponent<Dialog>();
        var sectionContent = innerDialog.RenderSectionContent(ctx);
        var bodyLayout = sectionContent.FindComponent<DialogBodyTextLayout>();

        // Assert
        bodyLayout.Markup.Should().Contain(body);
    }
}
