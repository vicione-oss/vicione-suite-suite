using Blazor.Server.Backend.Areas.Identity.Pages.Models;
using Core.Shared.Instance.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Account;

[AllowAnonymous]
public sealed partial class ChangePassword
{
    [Inject]
    private ILoginDesignService LoginDesignService { get; set; } = default!;

    [SupplyParameterFromForm]
    private ChangePasswordFormModel Input { get; set; } = default!;

    [SupplyParameterFromQuery]
    private string? User { get; set; }

    [SupplyParameterFromQuery]
    private bool IsPersistent { get; set; }

    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }
}
