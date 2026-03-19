using Blazor.Shared.Services;
using Core.Shared.Instance.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Instance;

namespace Blazor.Shared.Components;

public sealed partial class AccountLayout
{
    [Inject]
    private IInstanceInformationProvider InstanceInformation { get; set; } = default!;

    [Inject]
    private CopyrightYearProvider CopyrightYearProvider { get; set; } = default!;

    [Parameter]
    public RenderFragment? Heading { get; set; }

    [Parameter]
    public RenderFragment? LeftSideContent { get; set; }

    [Inject]
    private ILoginDesignService LoginDesignService { get; set; } = default!;
}
