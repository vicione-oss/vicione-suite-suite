using Blazor.Shared.Wizards.Models;
using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Wizards.Components;

public sealed partial class WizardStepper : ComponentBase
{
    private IReadOnlyList<WizardStep> _steps = [];
    private WizardStep? _activeStep;
    private bool _shouldRender;

    [Parameter, EditorRequired] public IReadOnlyList<WizardStep> Steps { get; set; }
    [Parameter, EditorRequired] public WizardStep? ActiveStep { get; set; }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (Steps != _steps || Steps.Intersect(_steps).Count() != Steps.Count)
        {
            _steps = Steps;

            _shouldRender = true;
        }

        if (ActiveStep != _activeStep)
        {
            _activeStep = ActiveStep;

            _shouldRender = true;
        }
    }

    protected override bool ShouldRender()
    {
        if (_shouldRender)
        {
            _shouldRender = false;
            return true;
        }

        return false;
    }

    internal static string GetStepCssClass(IReadOnlyList<int> visibleStepNumbers, int index, int activeStepNumber)
    {
        var stepNumber = visibleStepNumbers[index];

        var result = new List<string> { "step" };

        if (activeStepNumber == stepNumber)
            result.Add("active");

        if (activeStepNumber > stepNumber)
            result.Add("done");

        // Only the outermost visible steps show a stack; the stack points toward the steps hidden next to them.
        if (index == 0 && visibleStepNumbers.Count > 1 && visibleStepNumbers[1] > stepNumber + 1)
            result.Add("stacked-after");

        if (index > 0 && index == visibleStepNumbers.Count - 1 && visibleStepNumbers[index - 1] < stepNumber - 1)
            result.Add("stacked-before");

        return string.Join(' ', result);
    }

    private static string GetStepSeparatorCssClass(int stepNumber, int previousStepNumber, int activeStepNumber)
    {
        var result = new List<string> { "separator" };

        if (previousStepNumber + 1 < stepNumber)
            result.Add("dotted");

        if (stepNumber <= activeStepNumber)
            result.Add("done");

        return string.Join(' ', result);
    }
}
