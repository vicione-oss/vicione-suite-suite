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

public sealed class OkDialogTests
{
    [Fact]
    public async Task ComponentGetsRendered()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();

        // Act
        var cut = ctx.Render<OkDialog>();

        // Assert
        Assert.NotNull(cut);
    }

    [Fact]
    public async Task Should_Be_Rendered_Hidden()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();

        // Act
        var cut = ctx.Render<OkDialog>();

        // Assert
        var innerDialog = cut.FindComponent<Dialog>();

        Assert.Throws<ComponentNotFoundException>(() => innerDialog.RenderSectionContent(ctx));
    }

    [Fact]
    public async Task Should_Be_Rendered_Visible()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();

        // Act
        var cut = ctx.Render<OkDialog>(parameters => parameters
            .Add(p => p.Visible, true));

        var innerDialog = cut.FindComponent<Dialog>();
        var sectionContent = innerDialog.RenderSectionContent(ctx);

        // Assert
        Assert.NotNull(sectionContent.Find(".ok-dialog"));
    }

    [Fact]
    public async Task OnConfirm_Event_Is_Fired_On_Ok_Button_Click()
    {
        // Arrange
        var onConfirmFired = false;
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();

        // Act
        var cut = ctx.Render<OkDialog>(parameters => parameters
            .Add(p => p.OnConfirm, () => { onConfirmFired = true; })
            .Add(p => p.Visible, true));

        var innerDialog = cut.FindComponent<Dialog>();
        var sectionContent = innerDialog.RenderSectionContent(ctx);
        var confirmButton = sectionContent.FindFooterButton(button => button.Text == "Ok");
        var innerButton = confirmButton.FindInnerButton();

        await innerButton.ClickAsync();

        // Assert
        Assert.True(onConfirmFired);
    }

    [Fact]
    public async Task OnConfirm_Event_Is_Fired_On_Close_Button_Click()
    {
        // Arrange
        var onConfirmFired = false;
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();

        // Act
        var cut = ctx.Render<OkDialog>(parameters => parameters
            .Add(p => p.OnConfirm, () => { onConfirmFired = true; })
            .Add(p => p.Visible, true));

        var innerDialog = cut.FindComponent<Dialog>();
        var sectionContent = innerDialog.RenderSectionContent(ctx);
        var closeButton = sectionContent.FindComponent<PopupHeaderCloseActionButton>();
        var innerButton = closeButton.Find("button");

        await innerButton.ClickAsync();

        // Assert
        Assert.True(onConfirmFired);
    }

    [Fact]
    public async Task Header_Text_Is_Passed_To_Dialog()
    {
        // Arrange
        var headerText = "Test header";
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();

        // Act
        var cut = ctx.Render<OkDialog>(parameters => parameters
            .Add(p => p.HeaderText, headerText)
            .Add(p => p.Visible, true));

        var innerDialog = cut.FindComponent<Dialog>();

        // Assert
        innerDialog.Instance.HeaderText.Should().Be(headerText);
    }

    [Fact]
    public async Task Body_Is_Rendered()
    {
        // Arrange
        var body = "<p>Test body</p>";
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();

        // Act
        var cut = ctx.Render<OkDialog>(parameters => parameters
            .Add(p => p.Body, body)
            .Add(p => p.Visible, true));

        var innerDialog = cut.FindComponent<Dialog>();
        var sectionContent = innerDialog.RenderSectionContent(ctx);
        var bodyLayout = sectionContent.FindComponent<DialogBodyTextLayout>();

        // Assert
        bodyLayout.Markup.Should().Contain(body);
    }
}
