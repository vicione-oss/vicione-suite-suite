using Blazor.Shared.Onboarding.Models;
using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Onboarding.Components.WizardPages;

public sealed partial class SummaryNetworkInterfaceDetails
{
    [Parameter, EditorRequired] public INetworkInterfaceConfiguration NetworkInterfaceConfiguration { get; set; }
}
