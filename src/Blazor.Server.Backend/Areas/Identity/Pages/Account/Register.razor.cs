using Blazor.Server.Backend.Areas.Identity.Pages.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Account;

[AllowAnonymous]
public sealed partial class Register
{
    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [SupplyParameterFromForm]
    private RegisterFormModel Input { get; set; } = new();

    public void RegisterUser()
    {

    }
}
