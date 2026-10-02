using Blazor.Server.Backend.Components;
using Bunit;
using ViciOne.Ui.MonochromeIcons.Core.Enums;

namespace Blazor.Server.Tests.Components;

public sealed class AccountButtonTests
{
    [Fact]
    public void Should_render_the_icon_beside_the_label_when_an_icon_is_given()
    {
        // Arrange
        using var ctx = new BunitContext();

        // Act
        var component = ctx.Render<AccountButton>(builder => builder
            .Add(c => c.Text, "Login with passkey")
            .Add(c => c.IconName, MonochromeIconName.PassKeyLight));

        // Assert
        component.Find("button").ClassList.Should().Contain("with-icon");
        component.Find("i").ClassList.Should().Contain("monochrome-icon-pass-key-light");
        component.Find("span.text").TextContent.Should().Be("Login with passkey");
    }

    [Fact]
    public void Should_render_the_label_alone_when_no_icon_is_given()
    {
        // Arrange
        using var ctx = new BunitContext();

        // Act
        var component = ctx.Render<AccountButton>(builder => builder
            .Add(c => c.Text, "Login"));

        // Assert
        component.FindAll("i").Should().BeEmpty();
        component.Find("button").GetAttribute("class").Should().Be("account-button default");
    }
}
