using Blazor.Server.Backend.Areas.Identity.Pages.Models;
using Core.Shared.Instance.Services;
using Microsoft.AspNetCore.Components;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Account;

public sealed partial class SetPassword
{
    [Inject]
    private ILoginDesignService LoginDesignService { get; set; } = default!;

    [SupplyParameterFromForm]
    private SetPasswordFormModel Input { get; set; } = default!;
}
