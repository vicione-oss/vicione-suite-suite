using Blazor.Server.Backend.Areas.Identity.Pages.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Account;

[AllowAnonymous]
public sealed partial class ForgotPassword
{
    [SupplyParameterFromForm]
    private ForgotPasswordFormModel Input { get; set; } = new();

    public void ResetPassword()
    {
    }
}
