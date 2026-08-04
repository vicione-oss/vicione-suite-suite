using Blazor.Shared.MessageBanner.NotificationArea;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.MessageBanner.Contracts;
using Sdk.Testing.Extensions;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;
using ViciOne.Ui.MonochromeIcons.Core.Factories;

namespace Blazor.Shared.Tests.MessageBanner;

public sealed class MessageBannerNotificationElementIconTests
{
    [Fact]
    public void Should_render_component()
    {
        // Arrange
        var state = new MessageBannerNotificationElementIconState();

        using var ctx = new BunitContext();
        ctx.Services.AddScoped(_ => state);

        // Act
        var renderedComponent = ctx.Render<MessageBannerNotificationElementIcon>();

        // Assert
        renderedComponent.Should().NotBeNull();
    }

    [Fact]
    public void Should_render_icon()
    {
        // Arrange
        var state = new MessageBannerNotificationElementIconState { Icon = MonochromeIconName.InfoLight };

        using var ctx = new BunitContext();
        ctx.Services.AddScoped(_ => state);

        // Act
        var renderedComponent = ctx.Render<MessageBannerNotificationElementIcon>();

        // Assert
        var icon = renderedComponent.Find("i");
        icon.GetAttribute("class").Should().ContainAll(MonochromeIconName.InfoLight.GetCssClasses(MonochromeIconSize.Medium));
    }

    [Fact]
    public async Task Should_trigger_changed_event_on_outer_end_update()
    {
        // Arrange
        var stateChangedCounter = 0;

        var state = new MessageBannerNotificationElementIconState();
        state.Changed += () => ++stateChangedCounter;

        static async Task RandomUpdateTask(Random random, MessageBannerNotificationElementIconState state)
        {
            var delay = random.Next(0, 100);
            await Task.Delay(delay);

            state.BeginUpdate();
            try
            {
                var coinToss = random.Next(0, 2);

                if (coinToss == 0)
                {
                    var iconNames = TypeSafeEnumFactory<MonochromeIconName>.CreateAll();

                    state.Icon = iconNames.ElementAt(random.Next(iconNames.Count - 1));
                }
                else
                {
                    state.MessageType = random.NextEnum<MessageType>();
                }
            }
            finally
            {
                state.EndUpdate();
            }
        }

        // Act
        state.BeginUpdate();
        try
        {
            var random = new Random();

            var updateTasks = new List<Task>();
            for (var i = 0; i < 1000; i++)
                updateTasks.Add(RandomUpdateTask(random, state));

            await Task.WhenAll(updateTasks);
        }
        finally
        {
            state.EndUpdate();
        }

        // Assert
        stateChangedCounter.Should().Be(1);
    }
}
