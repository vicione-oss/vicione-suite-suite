using AwesomeAssertions;
using Blazor.Shared.Wizards.Models;
using Blazor.Shared.Wizards.Services;
using NSubstitute;
using Sdk.Client.Wizards.Services;
using Sdk.Testing.Client.Interfaces;
using Xunit;

namespace Blazor.Shared.Tests.Wizards.Services;

public sealed class WizardStateTests
{
    private readonly IHasChangeablePropertiesTests<WizardState> _tests = new();

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void Assert_changed_event_handling_when_loading_overlay_visible_is_set(bool initialValue, bool value, bool shouldTriggerChangedEvent)
        => _tests.AssertChangedEventHandlingWhenPropertyIsSet(state => state.LoadingOverlayVisible, initialValue, value, shouldTriggerChangedEvent);

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Assert_changed_event_handling_when_steps_is_set(bool sameInstance, bool shouldTriggerChangedEvent)
    {
        var initialSteps = new List<WizardStep>();
        var steps = sameInstance ? initialSteps : [];

        _tests.AssertChangedEventHandlingWhenPropertyIsSet(state => state.Steps, initialSteps, steps, shouldTriggerChangedEvent);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Assert_changed_event_handling_when_active_step_is_set(bool sameInstance, bool shouldTriggerChangedEvent)
    {
        var initialActiveStep = new WizardStep
        {
            Number = 1,
            Title = "Initial Active Step",
            WizardPageRegistryItem = Substitute.For<IWizardPageRegistryItem>()
        };

        var activeStep = sameInstance ? initialActiveStep : new WizardStep
        {
            Number = 2,
            Title = "New Active Step",
            WizardPageRegistryItem = Substitute.For<IWizardPageRegistryItem>()
        };

        _tests.AssertChangedEventHandlingWhenPropertyIsSet(state => state.ActiveStep, initialActiveStep, activeStep, shouldTriggerChangedEvent);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void Assert_changed_event_handling_when_back_button_enabled_is_set(bool initialValue, bool value, bool shouldTriggerChangedEvent)
        => _tests.AssertChangedEventHandlingWhenPropertyIsSet(state => state.BackButtonEnabled, initialValue, value, shouldTriggerChangedEvent);

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void Assert_changed_event_handling_when_next_button_enabled_is_set(bool initialValue, bool value, bool shouldTriggerChangedEvent)
        => _tests.AssertChangedEventHandlingWhenPropertyIsSet(state => state.NextButtonEnabled, initialValue, value, shouldTriggerChangedEvent);

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void Assert_changed_event_handling_when_finish_button_enabled_is_set(bool initialValue, bool value, bool shouldTriggerChangedEvent)
        => _tests.AssertChangedEventHandlingWhenPropertyIsSet(state => state.FinishButtonEnabled, initialValue, value, shouldTriggerChangedEvent);

    [Fact]
    public void Should_not_trigger_changed_event_after_begin_update()
    {
        // Arrange
        var state = new WizardState();

        var stateChanged = false;

        state.Changed += args => stateChanged = true;

        // Act
        state.BeginUpdate();

        state.LoadingOverlayVisible = true;
        state.FinishButtonEnabled = true;

        // Assert
        stateChanged.Should().Be(false);
    }

    [Fact]
    public void Should_trigger_changed_event_on_end_update_with_correct_args()
    {
        // Arrange
        var state = new WizardState();

        var changedCounter = 0;
        var affectedPropertyNames = new List<string>();

        state.Changed += args =>
        {
            changedCounter++;
            affectedPropertyNames.AddRange(args.PropertyNames);
        };

        // Act
        state.BeginUpdate();
        try
        {
            state.LoadingOverlayVisible = true;
            state.FinishButtonEnabled = true;
        }
        finally
        {
            state.EndUpdate();
        }

        // Assert
        changedCounter.Should().Be(1);
        affectedPropertyNames.Should().BeEquivalentTo([nameof(state.LoadingOverlayVisible), nameof(state.FinishButtonEnabled)]);
    }

    [Fact]
    public async Task Should_trigger_changed_event_on_outer_end_update()
    {
        // Arrange
        var stateChangedCounter = 0;

        var state = new WizardState();
        state.Changed += args => ++stateChangedCounter;

        static async Task RandomUpdateTask(Random random, WizardState state)
        {
            var delay = random.Next(0, 100);
            await Task.Delay(delay);

            state.BeginUpdate();
            try
            {
                var coinToss = random.Next(0, 2);

                if (coinToss == 0)
                    state.LoadingOverlayVisible = !state.LoadingOverlayVisible;
                else
                    state.FinishButtonEnabled = !state.FinishButtonEnabled;
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
