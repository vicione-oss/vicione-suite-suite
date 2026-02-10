using Blazor.Shared.Dialogs;
using Blazor.Tests.Tools;
using Bunit;
using Xunit;

namespace Blazor.Shared.Tests.Dialogs;

public sealed class ErrorDialogTests
{
    [Fact]
    public async Task ComponentGetsRendered()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var cut = ctx.Render<ErrorDialog>();

        // Assert
        Assert.NotNull(cut);
    }

    [Fact]
    public async Task Shoul_Be_Rendered_Hidden()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var cut = ctx.Render<ErrorDialog>();

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
        var cut = ctx.Render<ErrorDialog>();
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
        var cut = ctx.Render<ErrorDialog>(parameters => parameters
            .Add(p => p.OnConfirm, () => { onConfirmFired = true; })
        );
        cut.Render(parameters => parameters.Add(p => p.Show, true));
        cut.WaitForElement("button", TimeSpan.FromSeconds(1));
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
        var cut = ctx.Render<ErrorDialog>(parameters => parameters
            .Add(p => p.OnConfirm, () => { onConfirmFired = true; })
        );
        cut.Render(parameters => parameters.Add(p => p.Show, true));
        cut.FindAll("button")[1].Click();

        // Assert
        Assert.NotNull(cut);
        Assert.True(onConfirmFired);
    }

    [Fact]
    public async Task Exception_Is_Rendered()
    {
        // Arrange
        var exceptionMessage = "Exception test message";
        var exception = new InvalidOperationException(exceptionMessage);
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var cut = ctx.Render<ErrorDialog>(parameters => parameters
            .Add(p => p.Exception, exception)
            .Add(p => p.IsDebugEnabled, true)
        );
        cut.Render(parameters => parameters.Add(p => p.Show, true));
        var bodyContainer = cut.Find(".content-container");

        // Assert
        Assert.NotNull(cut);
        Assert.Contains(exception.GetType().ToString(), bodyContainer.InnerHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(exceptionMessage, bodyContainer.InnerHtml, StringComparison.OrdinalIgnoreCase);
    }
}
