using Blazor.Shared.MessageBanner.Components;

namespace Blazor.Shared.Tests.MessageBanner;

public sealed class MessageBannerDialogStateTests
{
    [Fact]
    public void Should_raise_changed_event_when_message_is_set()
    {
        // Arrange
        var state = new MessageBannerDialogState();
        var stateMonitor = state.Monitor();

        var message = new DummyMessage();

        // Act
        state.Message = message;

        // Assert
        stateMonitor.Should().Raise(nameof(state.Changed));
    }

    [Fact]
    public void Should_raise_changed_event_when_visible_is_set()
    {
        // Arrange
        var state = new MessageBannerDialogState();
        var stateMonitor = state.Monitor();

        // Act
        state.Visible = true;

        // Assert
        stateMonitor.Should().Raise(nameof(state.Changed));
    }

    [Fact]
    public void Should_not_trigger_changed_event_after_begin_update()
    {
        // Arrange
        var state = new MessageBannerDialogState();
        var stateMonitor = state.Monitor();

        // Act
        state.BeginUpdate();

        state.Visible = false;
        state.Message = new DummyMessage();

        // Assert
        stateMonitor.Should().NotRaise(nameof(state.Changed));
    }

    [Fact]
    public void Should_trigger_changed_event_on_end_update()
    {
        // Arrange
        var state = new MessageBannerDialogState();

        var changedCounter = 0;

        state.Changed += () => changedCounter++;

        // Act
        state.BeginUpdate();
        try
        {
            state.Visible = false;
            state.Message = new DummyMessage();
        }
        finally
        {
            state.EndUpdate();
        }

        // Assert
        changedCounter.Should().Be(1);
    }

    [Fact]
    public async Task Should_trigger_changed_event_on_outer_end_update()
    {
        // Arrange
        var stateChangedCounter = 0;

        var state = new MessageBannerDialogState();
        state.Changed += () => ++stateChangedCounter;

        static async Task RandomUpdateTask(Random random, MessageBannerDialogState state)
        {
            var delay = random.Next(0, 100);
            await Task.Delay(delay);

            state.BeginUpdate();
            try
            {
                var coinToss = random.Next(0, 2);

                if (coinToss == 0)
                    state.Visible = !state.Visible;
                else
                    state.Message = new DummyMessage();
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
