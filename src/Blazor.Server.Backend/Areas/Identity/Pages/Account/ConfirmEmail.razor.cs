using System.Text;
using Blazor.Shared.Services;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Account;

public sealed partial class ConfirmEmail
{
    private bool? _wasSuccessful;

    [Inject]
    private INavigationService NavigationService { get; set; } = default!;
    [Inject]
    private UserManager<SuiteUser> UserManager { get; set; } = default!;

    [SupplyParameterFromQuery]
    private string? UserId { get; set; }
    [SupplyParameterFromQuery]
    private string? Code { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (string.IsNullOrWhiteSpace(UserId) || string.IsNullOrWhiteSpace(Code))
        {
            NavigationService.RedirectTo(IdentityConstants.LoginRoute);
            return;
        }
        var user = await UserManager.FindByIdAsync(UserId);
        if (user == null)
            return;

        var code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Code));
        var result = await UserManager.ConfirmEmailAsync(user, code);
        _wasSuccessful = result.Succeeded;
    }
}
