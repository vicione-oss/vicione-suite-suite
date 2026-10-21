using Core.Shared.Instance.Services;
using Core.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.RateLimiting;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Account;

[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.SignIn)]
public sealed partial class ForgotPassword
{
    [Inject]
    private ILoginDesignService LoginDesignService { get; set; } = default!;
}
