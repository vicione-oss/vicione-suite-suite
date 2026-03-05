using Blazor.Server.Backend.Areas.Identity.Pages.Models;
using Blazor.Server.Backend.Security;
using Blazor.Shared.Services;
using Core.Shared.Mail;
using Core.Shared.Security;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;

#pragma warning disable 8618 //required properties are not null!

namespace Blazor.Server.Backend.Areas.Identity.Pages.Account;

[AllowAnonymous]
public sealed partial class Login
{
    private EditContext editContext = default!;
    private ValidationMessageStore? messageStore;
    private string _externalProviderDisplayName;
    private string _externalProviderName;

    private int? _externalError { get; set; }

    [Inject]
    private SignInManager<SuiteUser> SignInManager { get; set; }
    [Inject]
    private UserManager<SuiteUser> UserManager { get; set; }
    [Inject]
    private IAccountVerification AccountVerification { get; set; }
    [Inject]
    private ISuiteMediator Mediator { get; set; }
    [Inject]
    private IMailSenderStatus SenderStatus { get; set; }
    [Inject]
    private INavigationService NavigationService { get; set; }
    [Inject]
    private IExternalAuthenticationSettings ExternalAuthenticationSettings { get; set; }
    [Inject]
    private ILogger<Login> Logger { get; set; }

    [SupplyParameterFromForm]
    private LoginFormModel Input { get; set; }

    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    private ExternalLoginError ExternalErrorTyped
        => _externalError.HasValue ? (ExternalLoginError)_externalError : ExternalLoginError.None;

    public bool IsExternalIdProviderConfigured { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Input ??= new();
        editContext = new EditContext(Input);
        editContext.OnValidationRequested += HandleValidationRequested;
        messageStore = new(editContext);


        await InitializeExternalIdProvider();

        if (HttpMethods.IsGet(HttpContext.Request.Method))
        {
            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme);
        }
    }

    private void HandleValidationRequested(object? _, ValidationRequestedEventArgs args)
        => messageStore?.Clear();

    public async Task LoginUser()
    {
        ReturnUrl = "/";

        if (!editContext.Validate())
        {
            return;
        }

        if (ExternalErrorTyped > ExternalLoginError.None)
            messageStore?.Add(() => Input.Username, MapToErrorMessage(ExternalErrorTyped));

        var user = await UserManager.FindByNameAsync(Input.Username);
        if (user == null)
        {
            messageStore?.Add(() => Input.Username, Localization.Login.UserOrPasswordIsIncorrect);
            messageStore?.Add(() => Input.Password, Localization.Login.UserOrPasswordIsIncorrect);
            return;
        }

        if (user.UserName is null)
        {
            messageStore?.Add(() => Input.Username, Localization.Login.UserOrPasswordIsIncorrect);
            messageStore?.Add(() => Input.Password, Localization.Login.UserOrPasswordIsIncorrect);
            return;
        }

        // This doesn't count login failures towards account lockout
        // To enable password failures to trigger account lockout, set lockoutOnFailure: true
        var result = await SignInManager.PasswordSignInAsync(user.UserName,
            Input.Password,
            Input.RememberMe,
            lockoutOnFailure: false);

        if (result.Succeeded)
        {
            Logger.LogInformation("User logged in");

            if (user.PasswordExpirationDate <= DateTimeOffset.UtcNow)
            {
                var username = user.UserName;
                await SignInManager.SignOutAsync();

                NavigationService.RedirectTo(
                    "/account/change-password",
                    new Dictionary<string, object?>()
                    {
                        { "user", username },
                        { "isPersistent", Input.RememberMe }
                    });
            }

            NavigationService.NavManager.NavigateTo(ReturnUrl);
            return;
        }

        if (result.IsNotAllowed && await IsAccountVerificationNeeded(user.Id))
        {
            Logger.LogInformation("User logged in but is not yet verified");
            var code = await UserManager.GenerateEmailConfirmationTokenAsync(user);
            await SendVerificationEmail(user, code);

            NavigationService.RedirectTo(IdentityConstants.EmailConfirmationRoute);
            return;
        }

        if (result.IsLockedOut)
        {
            Logger.LogWarning("User account locked out");
            NavigationService.RedirectTo("./Lockout");
            return;
        }

        messageStore?.Add(() => Input.Username, Localization.Login.UserOrPasswordIsIncorrect);
        messageStore?.Add(() => Input.Password, Localization.Login.UserOrPasswordIsIncorrect);
        return;
    }

    private static string MapToErrorMessage(ExternalLoginError externalError)
        => externalError switch
        {
            ExternalLoginError.LoginFailed => Localization.Login.ExternalLoginFailed,
            ExternalLoginError.NoLocalUser => Localization.Login.NoLocalUser,
            ExternalLoginError.UnknownExternalUser => Localization.Login.UnknownExternalUser,
            ExternalLoginError.None => string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(externalError), externalError, null)
        };

    private async Task InitializeExternalIdProvider()
    {
        IsExternalIdProviderConfigured
            = await ExternalAuthenticationSettings.IsExternalAuthenticationProviderConfigured();

        if (!IsExternalIdProviderConfigured)
            return;

        _externalProviderDisplayName = "OpenID";
        _externalProviderName = "OpenIdConnect";
    }

    private Task<bool> IsAccountVerificationNeeded(string userId)
        => AccountVerification
            .NeedsVerification(userId);

    private Task SendVerificationEmail(SuiteUser user, string code)
        => Mediator.Send(new SendVerifyEmailAddressLink(user.Id,
            NavigationService.CreateCallbackLink(IdentityConstants.ConfirmMailRoute, user.Id, code)));
}
