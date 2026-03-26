using Blazor.Server.Backend.Areas.Identity.Pages.Models;
using Blazor.Server.Backend.Localization;
using Blazor.Server.Backend.Security;
using Blazor.Shared.Services;
using Core.Shared.Mail;
using Core.Shared.Security;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;

namespace Blazor.Server.Backend.Components;

public sealed partial class LoginContent
{
    private EditContext editContext = default!;
    private ValidationMessageStore? messageStore;
    private string _externalProviderDisplayName = string.Empty;
    private string _externalProviderName = string.Empty;
    private readonly string _formId = "external-login";

    private int? _externalError { get; set; }

    [Inject]
    private SignInManager<SuiteUser> SignInManager { get; set; } = default!;

    [Inject]
    private UserManager<SuiteUser> UserManager { get; set; } = default!;

    [Inject]
    private IAccountVerification AccountVerification { get; set; } = default!;

    [Inject]
    private ISuiteMediator Mediator { get; set; } = default!;

    [Inject]
    private IMailSenderStatus SenderStatus { get; set; } = default!;

    [Inject]
    private INavigationService NavigationService { get; set; } = default!;

    [Inject]
    private IExternalAuthenticationSettings ExternalAuthenticationSettings { get; set; } = default!;

    [Inject]
    private ILogger<LoginContent> Logger { get; set; } = default!;

    [Parameter]
    public bool BuildingLayout { get; set; }

    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [SupplyParameterFromForm]
    private LoginFormModel Input { get; set; } = default!;

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
            messageStore?.Add(() => Input.Password, Login.UserOrPasswordIsIncorrect);
            messageStore?.Add(() => Input.Username, Login.UserOrPasswordIsIncorrect);
            return;
        }

        if (user.UserName is null)
        {
            messageStore?.Add(() => Input.Username, Login.UserOrPasswordIsIncorrect);
            messageStore?.Add(() => Input.Password, Login.UserOrPasswordIsIncorrect);
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
            LogUserLoggedIn(Logger);

            if (user.PasswordExpirationDate <= DateTimeOffset.UtcNow)
            {
                var username = user.UserName;
                await SignInManager.SignOutAsync();

                NavigationService.RedirectTo(
                    IdentityConstants.ChangePasswordRoute,
                    new Dictionary<string, object?>()
                    {
                        { "user", username },
                        { "isPersistent", Input.RememberMe }
                    });
            }

            try
            {
                NavigationService.NavManager.NavigateTo(ReturnUrl);
            }
            catch (NavigationException ex)
            {
                LogNavigationError(Logger, ex.Message);
            }
            return;
        }

        if (result.IsNotAllowed && await IsAccountVerificationNeeded(user.Id))
        {
            LogUserNotYetVerified(Logger);
            var code = await UserManager.GenerateEmailConfirmationTokenAsync(user);
            await SendVerificationEmail(user, code);

            NavigationService.RedirectTo(IdentityConstants.EmailConfirmationRoute);
            return;
        }

        if (result.IsLockedOut)
        {
            LogUserLockedOut(Logger);
            NavigationService.RedirectTo("./Lockout");
            return;
        }

        messageStore?.Add(() => Input.Username, Login.UserOrPasswordIsIncorrect);
        messageStore?.Add(() => Input.Password, Login.UserOrPasswordIsIncorrect);
        return;
    }

    private static string MapToErrorMessage(ExternalLoginError externalError)
        => externalError switch
        {
            ExternalLoginError.LoginFailed => Login.ExternalLoginFailed,
            ExternalLoginError.NoLocalUser => Login.NoLocalUser,
            ExternalLoginError.UnknownExternalUser => Login.UnknownExternalUser,
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

    [LoggerMessage(Level = LogLevel.Information, Message = "User logged in")]
    private static partial void LogUserLoggedIn(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Navigation error: {Message}")]
    private static partial void LogNavigationError(ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Information, Message = "User logged in but is not yet verified")]
    private static partial void LogUserNotYetVerified(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "User account locked out")]
    private static partial void LogUserLockedOut(ILogger logger);
}
