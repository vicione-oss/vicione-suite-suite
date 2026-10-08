using Blazor.Shared.Wizards.Components;

namespace Blazor.Shared.Tests.Wizards.Components;

public sealed class WizardStepperTests
{
    [Theory]
    [InlineData(new[] { 1, 2, 3, 4, 5, 12 }, 2, 5, "step stacked-before")]
    [InlineData(new[] { 1, 2, 3, 4, 5, 12 }, 2, 0, "step done")]
    [InlineData(new[] { 1, 4, 5, 6, 7, 12 }, 5, 0, "step done stacked-after")]
    [InlineData(new[] { 1, 4, 5, 6, 7, 12 }, 5, 1, "step done")]
    [InlineData(new[] { 1, 4, 5, 6, 7, 12 }, 5, 2, "step active")]
    [InlineData(new[] { 1, 4, 5, 6, 7, 12 }, 5, 5, "step stacked-before")]
    [InlineData(new[] { 1, 3, 4, 5, 6, 7 }, 7, 5, "step active")]
    [InlineData(new[] { 1, 2, 3, 4, 5, 6 }, 1, 0, "step active")]
    [InlineData(new[] { 1 }, 1, 0, "step active")]
    public void Should_mark_outermost_steps_next_to_hidden_steps_as_stacked(
        int[] visibleStepNumbers, int activeStepNumber, int index, string expected)
    {
        // Act
        var result = WizardStepper.GetStepCssClass(visibleStepNumbers, index, activeStepNumber);

        // Assert
        result.Should().Be(expected);
    }
}
