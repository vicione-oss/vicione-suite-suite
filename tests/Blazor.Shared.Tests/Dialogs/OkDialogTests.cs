using Blazor.Shared.Dialogs;
using Blazor.Tests.Tools;
using Bunit;
using AwesomeAssertions;
using Xunit;

namespace Blazor.Shared.Tests.Dialogs;

public sealed class OkDialogTests
{
    [Fact]
    public async Task ComponentGetsRendered()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServicesWithBlazorDx();

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
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var cut = ctx.Render<OkDialog>();

        // Assert
        Assert.NotNull(cut);
        Assert.Throws<ElementNotFoundException>(() => cut.Find(".dxbs-popup"));
    }

    [Fact]
    public async Task Should_Be_Rendered_Visible()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var cut = ctx.Render<OkDialog>();
        cut.Render(parameters => parameters.Add(p => p.Show, true));

        // Assert
        Assert.NotNull(cut);
        Assert.NotNull(cut.Find(".dxbl-popup"));
    }

    [Fact]
    public async Task OnConfirm_Event_Is_Fired_On_Ok_Button_Click()
    {
        // Arrange
        var onConfirmFired = false;
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var cut = ctx.Render<OkDialog>(parameters => parameters
            .Add(p => p.OnConfirm, () => { onConfirmFired = true; })
        );
        cut.Render(parameters => parameters.Add(p => p.Show, true));
        cut.Instance.Refresh();
        cut.Find(".btn-footer").Click();

        // Assert
        Assert.NotNull(cut);
        Assert.True(onConfirmFired);
    }

    [Fact]
    public async Task OnConfirm_Event_Is_Fired_On_Cross_Button_Click()
    {
        // Arrange
        var onConfirmFired = false;
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var cut = ctx.Render<OkDialog>(parameters => parameters
            .Add(p => p.OnConfirm, () => { onConfirmFired = true; })
        );
        cut.Render(parameters => parameters.Add(p => p.Show, true));
        cut.FindAll("button")[1].Click();

        // Assert
        Assert.NotNull(cut);
        Assert.True(onConfirmFired);
    }

    [Fact]
    public async Task Header_Is_Rendered()
    {
        // Arrange
        var headerText = "Test header";
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var cut = ctx.Render<OkDialog>(parameters => parameters
            .Add(p => p.HeaderText, headerText)
        );
        cut.Render(parameters => parameters.Add(p => p.Show, true));
        var headerElement = cut.Find(".header-bar");

        // Assert
        Assert.NotNull(cut);
        headerElement.InnerHtml.Should().Contain(headerText);
    }

    [Fact]
    public async Task Body_Is_Rendered()
    {
        // Arrange
        var body = "<p>Test body</p>";
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var cut = ctx.Render<OkDialog>(parameters => parameters
            .Add(p => p.Body, body)
        );
        cut.Render(parameters => parameters.Add(p => p.Show, true));
        var bodyContainer = cut.Find(".content-container");

        // Assert
        Assert.NotNull(cut);
        Assert.Contains(body, bodyContainer.InnerHtml, StringComparison.OrdinalIgnoreCase);
    }
}
