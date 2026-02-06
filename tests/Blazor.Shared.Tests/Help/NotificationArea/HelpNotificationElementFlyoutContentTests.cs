using Blazor.Shared.Help.NotificationArea;
using Bunit;
using AwesomeAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Sdk.Testing.Client;
using Xunit;

namespace Blazor.Shared.Tests.Help.NotificationArea;

public sealed class HelpNotificationElementFlyoutContentTests
{
    [Fact]
    public void Component_should_render()
    {
        // Arrange
        using var ctx = new BunitContext();
        ctx.SetupSuiteServices();

        // Act
        var component = ctx.Render<HelpNotificationElementFlyoutContent>();

        // Assert
        Assert.NotNull(component);
    }

    public class GetHelpItems
    {
        [Fact]
        public void Shows_all_help_items()
        {
            using var ctx = new BunitContext();
            ctx.SetupSuiteServices();

            var component = ctx.Render<HelpNotificationElementFlyoutContent>();

            component.Markup.Should().Contain("Help 1");
            component.Markup.Should().Contain("Help 2");
            component.Markup.Should().Contain("Help 3");
            component.Markup.Should().Contain("Help 4");
        }

        [Fact]
        public async Task Shows_filtered_help_itemsAsync()
        {
            await using var ctx = new BunitContext();
            ctx.SetupSuiteServices();

            var component = ctx.Render<HelpNotificationElementFlyoutContent>();
            var textbox = component.Find(".text-box");
            var args = new ChangeEventArgs()
            {
                Value = "2"
            };
            await textbox.InputAsync(args);
            await textbox.KeyUpAsync(new KeyboardEventArgs());

            component.Markup.Should().NotContain("Help 1");
            component.Markup.Should().Contain("Help 2");
            component.Markup.Should().NotContain("Help 3");
            component.Markup.Should().NotContain("Help 4");
        }
    }

    public class OnArrowBackClick
    {
        [Fact]
        public void Shows_previous_help()
        {
            using var ctx = new BunitContext();
            ctx.SetupSuiteServices();

            var component = ctx.Render<HelpNotificationElementFlyoutContent>();

            component.FindAll(".link-button--small").First(b => b.InnerHtml.Contains("read more", StringComparison.OrdinalIgnoreCase)).Click();
            component.Find(".link").Click();
            component.Find(".arrow-back").Click();

            component.Markup.Should().Contain("Help 1");
            component.Markup.Should().NotContain("Help 2");
            component.Markup.Should().NotContain("Help 3");
            component.Markup.Should().NotContain("Help 4");
        }

        [Fact]
        public void Shows_card_view()
        {
            using var ctx = new BunitContext();
            ctx.SetupSuiteServices();

            var component = ctx.Render<HelpNotificationElementFlyoutContent>();

            component.FindAll(".link-button--small").First(b => b.InnerHtml.Contains("read more")).Click();
            component.Find(".arrow-back").Click();

            component.Markup.Should().Contain("Help 1");
            component.Markup.Should().Contain("Help 2");
            component.Markup.Should().Contain("Help 3");
            component.Markup.Should().Contain("Help 4");
        }
    }

    public class OnHomeClick
    {
        [Fact]
        public void Should_redirect_to_listed_help_items()
        {
            using var ctx = new BunitContext();
            ctx.SetupSuiteServices();

            var component = ctx.Render<HelpNotificationElementFlyoutContent>();

            component.FindAll(".link-button--small").First(b => b.InnerHtml.Contains("read more", StringComparison.OrdinalIgnoreCase)).Click();
            component.Find(".link").Click();
            component.Find(".home").Click();

            component.Markup.Should().Contain("Help 1");
            component.Markup.Should().Contain("Help 2");
            component.Markup.Should().Contain("Help 3");
            component.Markup.Should().Contain("Help 4");
        }

        [Fact]
        public async Task Should_reset_filter()
        {
            await using var ctx = new BunitContext();
            ctx.SetupSuiteServices();

            var component = ctx.Render<HelpNotificationElementFlyoutContent>();
            var textbox = component.Find(".text-box");
            var args = new ChangeEventArgs()
            {
                Value = "2"
            };
            await textbox.InputAsync(args);
            await textbox.KeyUpAsync(new KeyboardEventArgs());

            component.Find(".home").Click();

            component.Markup.Should().Contain("Help 1");
            component.Markup.Should().Contain("Help 2");
            component.Markup.Should().Contain("Help 3");
            component.Markup.Should().Contain("Help 4");
        }
    }

    public class OnLinkClick
    {
        [Fact]
        public void Should_open_linked_help_detail()
        {
            using var ctx = new BunitContext();
            ctx.SetupSuiteServices();

            var component = ctx.Render<HelpNotificationElementFlyoutContent>();

            component.FindAll(".link-button--small").First(b => b.InnerHtml.Contains("read more", StringComparison.OrdinalIgnoreCase)).Click();
            component.Find(".link").Click();

            component.Markup.Should().NotContain("Help 1");
            component.Markup.Should().NotContain("Help 2");
            component.Markup.Should().Contain("Help 3");
            component.Markup.Should().NotContain("Help 4");
        }
    }

    public class OnHelpClick
    {
        [Fact]
        public void Should_open_help_detail()
        {
            using var ctx = new BunitContext();
            ctx.SetupSuiteServices();

            var component = ctx.Render<HelpNotificationElementFlyoutContent>();

            component.FindAll(".link-button--small").First(b => b.InnerHtml.Contains("read more")).Click();

            component.Markup.Should().Contain("text-container");
        }
    }

    public class OnCloseClick
    {
        [Fact]
        public void Removes_help_card()
        {
            using var ctx = new BunitContext();
            ctx.SetupSuiteServices();

            var component = ctx.Render<HelpNotificationElementFlyoutContent>();

            component.Find(".close").Click();

            component.Markup.Should().NotContain("Help 1");
            component.Markup.Should().Contain("Help 2");
            component.Markup.Should().Contain("Help 3");
            component.Markup.Should().Contain("Help 4");
        }
    }
}
