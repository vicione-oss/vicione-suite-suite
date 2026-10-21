using Blazor.Server.Backend.Areas.Identity.Pages.Models;
using Core.Shared.Instance.Services;
using Core.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.RateLimiting;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Account;

[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.SignIn)]
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
