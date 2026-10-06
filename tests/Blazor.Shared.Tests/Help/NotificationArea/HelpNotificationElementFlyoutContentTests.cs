using Blazor.Shared.Help.NotificationArea;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Sdk.Testing.Client;
using ViciOne.Ui.Blazor.Components.TestingHelpers.SearchBox.Extensions;

namespace Blazor.Shared.Tests.Help.NotificationArea;

public sealed class HelpNotificationElementFlyoutContentTests
{
    [Fact]
    public void Should_render_component()
    {
        // Arrange
        using var ctx = new BunitContext();
        ctx.SetupSuiteServices();
        ctx.JSInterop.SetupForSearchBox();

        // Act
        var component = ctx.Render<HelpNotificationElementFlyoutContent>();

        // Assert
        component.Should().NotBeNull();
    }

    public sealed class GetHelpItems
    {
        [Fact]
        public void Should_show_all_help_items()
        {
            // Arrange
            using var ctx = new BunitContext();
            ctx.SetupSuiteServices();
            ctx.JSInterop.SetupForSearchBox();

            // Act
            var component = ctx.Render<HelpNotificationElementFlyoutContent>();

            // Assert
            component.Markup.Should().Contain("Help 1");
            component.Markup.Should().Contain("Help 2");
            component.Markup.Should().Contain("Help 3");
            component.Markup.Should().Contain("Help 4");
        }

        [Fact]
        public async Task Should_show_filtered_help_items()
        {
            // Arrange
            await using var ctx = new BunitContext();
            ctx.SetupSuiteServices();
            ctx.JSInterop.SetupForSearchBox();

            var component = ctx.Render<HelpNotificationElementFlyoutContent>();
            var textbox = component.Find(".text-box");
            var args = new ChangeEventArgs()
            {
                Value = "2"
            };

            // Act
            await textbox.InputAsync(args);
            await textbox.KeyUpAsync(new KeyboardEventArgs());

            // Assert
            component.Markup.Should().NotContain("Help 1");
            component.Markup.Should().Contain("Help 2");
            component.Markup.Should().NotContain("Help 3");
            component.Markup.Should().NotContain("Help 4");
        }
    }

    public sealed class OnArrowBackClick
    {
        [Fact]
        public void Should_show_previous_help()
        {
            // Arrange
            using var ctx = new BunitContext();
            ctx.SetupSuiteServices();
            ctx.JSInterop.SetupForSearchBox();

            var component = ctx.Render<HelpNotificationElementFlyoutContent>();

            // Act
            component.FindAll(".link-button--small").First(b => b.InnerHtml.Contains("read more", StringComparison.OrdinalIgnoreCase)).Click();
            component.Find("a").Click();
            component.Find(".arrow-back").Click();

            // Assert
            component.Markup.Should().Contain("Help 1");
            component.Markup.Should().NotContain("Help 2");
            component.Markup.Should().NotContain("Help 3");
            component.Markup.Should().NotContain("Help 4");
        }

        [Fact]
        public void Should_show_card_view()
        {
            // Arrange
            using var ctx = new BunitContext();
            ctx.SetupSuiteServices();
            ctx.JSInterop.SetupForSearchBox();

            var component = ctx.Render<HelpNotificationElementFlyoutContent>();

            // Act
            component.FindAll(".link-button--small").First(b => b.InnerHtml.Contains("read more")).Click();
            component.Find(".arrow-back").Click();

            // Assert
            component.Markup.Should().Contain("Help 1");
            component.Markup.Should().Contain("Help 2");
            component.Markup.Should().Contain("Help 3");
            component.Markup.Should().Contain("Help 4");
        }
    }

    public sealed class OnHomeClick
    {
        [Fact]
        public void Should_redirect_to_listed_help_items()
        {
            // Arrange
            using var ctx = new BunitContext();
            ctx.SetupSuiteServices();
            ctx.JSInterop.SetupForSearchBox();

            var component = ctx.Render<HelpNotificationElementFlyoutContent>();

            // Act
            component.FindAll(".link-button--small").First(b => b.InnerHtml.Contains("read more", StringComparison.OrdinalIgnoreCase)).Click();
            component.Find("a").Click();
            component.Find(".home").Click();

            // Assert
            component.Markup.Should().Contain("Help 1");
            component.Markup.Should().Contain("Help 2");
            component.Markup.Should().Contain("Help 3");
            component.Markup.Should().Contain("Help 4");
        }

        [Fact]
        public async Task Should_reset_filter()
        {
            // Arrange
            await using var ctx = new BunitContext();
            ctx.SetupSuiteServices();
            ctx.JSInterop.SetupForSearchBox();

            var component = ctx.Render<HelpNotificationElementFlyoutContent>();
            var textbox = component.Find(".text-box");
            var args = new ChangeEventArgs()
            {
                Value = "2"
            };
            await textbox.InputAsync(args);
            await textbox.KeyUpAsync(new KeyboardEventArgs());

            // Act
            await component.Find(".home").ClickAsync();

            // Assert
            component.Markup.Should().Contain("Help 1");
            component.Markup.Should().Contain("Help 2");
            component.Markup.Should().Contain("Help 3");
            component.Markup.Should().Contain("Help 4");
        }
    }

    public sealed class OnLinkClick
    {
        [Fact]
        public void Should_open_linked_help_detail()
        {
            // Arrange
            using var ctx = new BunitContext();
            ctx.SetupSuiteServices();
            ctx.JSInterop.SetupForSearchBox();

            var component = ctx.Render<HelpNotificationElementFlyoutContent>();

            // Act
            component.FindAll(".link-button--small").First(b => b.InnerHtml.Contains("read more", StringComparison.OrdinalIgnoreCase)).Click();
            component.Find("a").Click();

            // Assert
            component.Markup.Should().NotContain("Help 1");
            component.Markup.Should().NotContain("Help 2");
            component.Markup.Should().Contain("Help 3");
            component.Markup.Should().NotContain("Help 4");
        }
    }

    public sealed class OnHelpClick
    {
        [Fact]
        public void Should_open_help_detail()
        {
            // Arrange
            using var ctx = new BunitContext();
            ctx.SetupSuiteServices();
            ctx.JSInterop.SetupForSearchBox();

            var component = ctx.Render<HelpNotificationElementFlyoutContent>();

            // Act
            component.FindAll(".link-button--small").First(b => b.InnerHtml.Contains("read more")).Click();

            // Assert
            component.Markup.Should().Contain("text-container");
        }
    }

    public sealed class OnCloseClick
    {
        [Fact]
        public void Should_remove_help_card()
        {
            // Arrange
            using var ctx = new BunitContext();
            ctx.SetupSuiteServices();
            ctx.JSInterop.SetupForSearchBox();

            var component = ctx.Render<HelpNotificationElementFlyoutContent>();

            // Act
            component.Find(".close").Click();

            // Assert
            component.Markup.Should().NotContain("Help 1");
            component.Markup.Should().Contain("Help 2");
            component.Markup.Should().Contain("Help 3");
            component.Markup.Should().Contain("Help 4");
        }
    }
}
