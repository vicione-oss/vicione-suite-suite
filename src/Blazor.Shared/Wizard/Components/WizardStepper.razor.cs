using Blazor.Shared.Wizard.Models;
using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Wizard.Components;

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

    private static string GetStepCssClass(int stepNumber, int activeStepNumber)
    {
        var result = new List<string> { "step" };

        if (activeStepNumber == stepNumber)
            result.Add("active");

        if (activeStepNumber > stepNumber)
            result.Add("done");

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
