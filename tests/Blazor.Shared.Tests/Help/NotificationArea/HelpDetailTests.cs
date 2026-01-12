using Blazor.Shared.Help.Contracts;
using Blazor.Shared.Help.NotificationArea;
using Bunit;
using AwesomeAssertions;
using Xunit;

namespace Blazor.Shared.Tests.Help.NotificationArea;

public sealed class HelpDetailTests
{
    public class OnLinkClick
    {
        [Fact]
        public void Invokes_event()
        {
            using var ctx = new TestContext();
            var invoked = false;
            var help = new HelpModel
            {
                Id = Guid.NewGuid(),
                Title = "Title",
                TeaserText = "Teaser",
                Text = "Text <link text:link helpId:00000000-0000-0000-0000-000000000001",
                TeaserImagePath = string.Empty,
            };

            var component = ctx.RenderComponent<HelpDetail>(parameters =>
            {
                parameters.Add(p => p.Help, help);
                parameters.Add(p => p.OnLinkClick, () => invoked = true);
            });

            component.Find(".link").Click();

            invoked.Should().BeTrue();
        }
    }
}
