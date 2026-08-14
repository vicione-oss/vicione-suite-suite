using Blazor.Shared.Help.Contracts;
using Blazor.Shared.Help.NotificationArea;
using Bunit;

namespace Blazor.Shared.Tests.Help.NotificationArea;

public sealed class HelpDetailTests
{
    public sealed class OnLinkClick
    {
        [Fact]
        public void Should_invoke_event()
        {
            // Arrange
            using var ctx = new BunitContext();
            var invoked = false;
            var help = new HelpModel
            {
                Id = Guid.NewGuid(),
                Title = "Title",
                TeaserText = "Teaser",
                Text = "Text <link text:link helpId:00000000-0000-0000-0000-000000000001",
            };

            var component = ctx.Render<HelpDetail>(parameters =>
            {
                parameters.Add(p => p.Help, help);
                parameters.Add(p => p.OnLinkClick, () => invoked = true);
            });

            // Act
            component.Find("a").Click();

            // Assert
            invoked.Should().BeTrue();
        }
    }
}
