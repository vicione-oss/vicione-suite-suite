using Blazor.Shared.Dialogs;
using Blazor.Tests.Tools;
using Bunit;
using AwesomeAssertions;
using Xunit;

namespace Blazor.Shared.Tests.Dialogs;

public sealed class ConfirmCancelDialogTests
{
    [Fact]
    public void ComponentGetsRendered()
    {
        // Arrange
        using var ctx = new BunitContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var cut = ctx.Render<ConfirmCancelDialog>();

        // Assert
        Assert.NotNull(cut);
    }

    [Fact]
    public void Should_Be_Rendered_Hidden()
    {
        // Arrange
        using var ctx = new BunitContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var cut = ctx.Render<ConfirmCancelDialog>();

        // Assert
        Assert.NotNull(cut);
        Assert.Throws<ElementNotFoundException>(() => cut.Find(".dxbs-popup"));
    }

    [Fact]
    public void Should_Be_Rendered_Visible()
    {
        // Arrange
        using var ctx = new BunitContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var cut = ctx.Render<ConfirmCancelDialog>();
        cut.Render(parameters => parameters.Add(p => p.Show, true));

        // Assert
        Assert.NotNull(cut);
        Assert.NotNull(cut.Find(".dxbl-popup"));
    }

    [Fact]
    public void OnConfirm_Event_Is_Fired_On_Confirmed_Button_Click()
    {
        // Arrange
        var onConfirmFired = false;
        var onCancelFired = false;
        using var ctx = new BunitContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var cut = ctx.Render<ConfirmCancelDialog>(parameters => parameters
            .Add(p => p.OnConfirm, () => { onConfirmFired = true; })
            .Add(p => p.OnCancel, () => { onCancelFired = true; })
        );
        cut.Render(parameters => parameters.Add(p => p.Show, true));
        cut.FindDialogConfirmButton().Click();

        // Assert
        Assert.NotNull(cut);
        Assert.True(onConfirmFired);
        Assert.False(onCancelFired);
    }

    [Fact]
    public void OnCancel_Event_Is_Fired_On_Cancel_Button_Click()
    {
        // Arrange
        var onConfirmFired = false;
        var onCancelFired = false;
        using var ctx = new BunitContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var cut = ctx.Render<ConfirmCancelDialog>(parameters => parameters
            .Add(p => p.OnConfirm, () => { onConfirmFired = true; })
            .Add(p => p.OnCancel, () => { onCancelFired = true; })
        );
        cut.Render(parameters => parameters.Add(p => p.Show, true));
        cut.FindDialogCancelButton().Click();

        // Assert
        Assert.NotNull(cut);
        Assert.False(onConfirmFired);
        Assert.True(onCancelFired);
    }

    [Fact]
    public void OnCancel_Event_Is_Fired_On_Cross_Button_Click()
    {
        // Arrange
        var onConfirmFired = false;
        var onCancelFired = false;
        using var ctx = new BunitContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var cut = ctx.Render<ConfirmCancelDialog>(parameters => parameters
            .Add(p => p.OnConfirm, () => { onConfirmFired = true; })
            .Add(p => p.OnCancel, () => { onCancelFired = true; })
        );
        cut.Render(parameters => parameters.Add(p => p.Show, true));
        cut.FindDialogCancelButton().Click();

        // Assert
        Assert.NotNull(cut);
        Assert.False(onConfirmFired);
        Assert.True(onCancelFired);
    }

    [Fact]
    public void Header_Is_Rendered()
    {
        // Arrange
        var headerText = "Test header";
        using var ctx = new BunitContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var cut = ctx.Render<ConfirmCancelDialog>(parameters => parameters
            .Add(p => p.HeaderText, headerText));

        cut.Render(parameters => parameters.Add(p => p.Show, true));
        var headerElement = cut.Find(".dxbl-popup-header");

        // Assert
        Assert.NotNull(cut);
        headerElement.InnerHtml.Should().Contain(headerText);
    }

    [Fact]
    public void Body_Is_Rendered()
    {
        // Arrange
        var body = "<p>Test body</p>";
        using var ctx = new BunitContext();
        ctx.SetupSuiteServicesWithBlazorDx();

        // Act
        var cut = ctx.Render<ConfirmCancelDialog>(parameters => parameters
            .Add(p => p.Body, body)
        );
        cut.Render(parameters => parameters.Add(p => p.Show, true));
        var bodyContainer = cut.Find(".content-container");

        // Assert
        Assert.NotNull(cut);
        Assert.Contains(body, bodyContainer.InnerHtml, StringComparison.OrdinalIgnoreCase);
    }
}
