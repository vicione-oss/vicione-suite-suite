using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Wizard.Components;

public interface IWizard : IComponent
{
    string Title { get; }
}
