using Blazor.Server.Backend.Areas.Identity.Pages.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Account;

[AllowAnonymous]
public sealed partial class ResendEmailConfirmation
{
    [SupplyParameterFromForm]
    private ResendEmailConfirmationFormModel Input { get; set; } = new();

    public void ResendEmail()
    {
    }
}
