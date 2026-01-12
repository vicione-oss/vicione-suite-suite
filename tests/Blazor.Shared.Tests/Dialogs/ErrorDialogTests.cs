using Blazor.Shared.Dialogs;
using Blazor.Tests.Tools;
using Bunit;
using Xunit;

namespace Blazor.Shared.Tests.Dialogs;

public sealed class ErrorDialogTests
{
    [Fact]
    public void ComponentGetsRendered()
    {
        // Arrange
        using var ctx = new TestContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var cut = ctx.RenderComponent<ErrorDialog>();

        // Assert
        Assert.NotNull(cut);
    }

    [Fact]
    public void Shoul_Be_Rendered_Hidden()
    {
        // Arrange
        using var ctx = new TestContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var cut = ctx.RenderComponent<ErrorDialog>();

        // Assert
        Assert.NotNull(cut);
        Assert.Throws<ElementNotFoundException>(() => cut.Find(".dxbs-popup"));
    }

    [Fact]
    public void Should_Be_Rendered_Visible()
    {
        // Arrange
        using var ctx = new TestContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var cut = ctx.RenderComponent<ErrorDialog>();
        cut.SetParametersAndRender(parameters => parameters.Add(p => p.Show, true));

        // Assert
        Assert.NotNull(cut);
        Assert.NotNull(cut.Find(".dxbl-popup"));
    }

    [Fact]
    public void OnConfirm_Event_Is_Fired_On_Ok_Button_Click()
    {
        // Arrange
        var onConfirmFired = false;
        using var ctx = new TestContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var cut = ctx.RenderComponent<ErrorDialog>(parameters => parameters
            .Add(p => p.OnConfirm, () => { onConfirmFired = true; })
        );
        cut.SetParametersAndRender(parameters => parameters.Add(p => p.Show, true));
        cut.WaitForElement("button", TimeSpan.FromSeconds(1));
        cut.Find(".btn-footer").Click();

        // Assert
        Assert.NotNull(cut);
        Assert.True(onConfirmFired);
    }

    [Fact]
    public void OnConfirm_Event_Is_Fired_On_Cross_Button_Click()
    {
        // Arrange
        var onConfirmFired = false;
        using var ctx = new TestContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var cut = ctx.RenderComponent<ErrorDialog>(parameters => parameters
            .Add(p => p.OnConfirm, () => { onConfirmFired = true; })
        );
        cut.SetParametersAndRender(parameters => parameters.Add(p => p.Show, true));
        cut.FindAll("button")[1].Click();

        // Assert
        Assert.NotNull(cut);
        Assert.True(onConfirmFired);
    }

    [Fact]
    public void Exception_Is_Rendered()
    {
        // Arrange
        var exceptionMessage = "Exception test message";
        var exception = new InvalidOperationException(exceptionMessage);
        using var ctx = new TestContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var cut = ctx.RenderComponent<ErrorDialog>(parameters => parameters
            .Add(p => p.Exception, exception)
            .Add(p => p.IsDebugEnabled, true)
        );
        cut.SetParametersAndRender(parameters => parameters.Add(p => p.Show, true));
        var bodyContainer = cut.Find(".content-container");

        // Assert
        Assert.NotNull(cut);
        Assert.Contains(exception.GetType().ToString(), bodyContainer.InnerHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(exceptionMessage, bodyContainer.InnerHtml, StringComparison.OrdinalIgnoreCase);
    }
}
