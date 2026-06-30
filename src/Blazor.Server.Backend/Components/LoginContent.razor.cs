using System.Diagnostics.CodeAnalysis;
using Blazor.Server.Backend.Areas.Identity.Pages.Models;
using Blazor.Server.Backend.Localization;
using Blazor.Server.Backend.Security;
using Blazor.Shared;
using Blazor.Shared.Services;
using Core.Shared.Mail;
using Core.Shared.Passkeys;
using Core.Shared.Security;
using Core.Shared.UserManagement.Configuration;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;
using Sdk.Backend.Messaging;

namespace Blazor.Server.Backend.Components;

public sealed partial class LoginContent
{
    private EditContext editContext = default!;
    private ValidationMessageStore messageStore = null!;
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

    [Inject]
    public IFeatureManager FeatureManager { get; set; } = default!;

    [Inject]
    private IPasskeyHostSupport PasskeyHostSupport { get; set; } = default!;

    [Inject]
    public ITempDataDictionaryFactory TempDataFactory { get; set; } = default!;

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

    private bool IsExternalIdProviderConfigured { get; set; }

    private bool IsPasskeySignInEnabled { get; set; }

    private bool IsPasskeyHostSupported { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Input ??= new();
        editContext = new EditContext(Input);
        editContext.OnValidationRequested += HandleValidationRequested;
        messageStore = new(editContext);

        IsPasskeySignInEnabled = await FeatureManager.IsEnabledAsync(Core.Shared.Features.Constants.PasskeyFeatureName);
        IsPasskeyHostSupported = PasskeyHostSupport.IsPasskeyCapableHost(HttpContext?.Request.Host.Host);

        await InitializeExternalIdProvider();

        // HttpContext is only present during static server rendering. It is null in the
        // brief interactive re-render that occurs while navigating away after sign-in,
        // where TempData and external sign-out are neither available nor needed.
        if (HttpContext is null)
            return;

        ReadExternalErrorFromTempData();

        if (ExternalErrorTyped > ExternalLoginError.None)
            messageStore?.Add(() => Input.Username, MapToErrorMessage(ExternalErrorTyped));

        if (HttpMethods.IsGet(HttpContext.Request.Method))
        {
            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme);
        }
    }

    private void ReadExternalErrorFromTempData()
    {
        var tempData = TempDataFactory.GetTempData(HttpContext);
        if (tempData.TryGetValue(ExternalLoginErrorConstants.ExternalLoginErrorKey, out var message))
        {
            _externalError = message as int?;
            tempData.Remove(ExternalLoginErrorConstants.ExternalLoginErrorKey);
            tempData.Save();
        }
        else
        {
            _externalError = null;
        }
    }


    private void HandleValidationRequested(object? _, ValidationRequestedEventArgs args)
        => messageStore.Clear();

    public async Task LoginUser()
    {
        ReturnUrl ??= "/";

        var (result, isPasskeySignIn) = IsPasskeySignIn(Input)
            ? (await ExecutePasskeyLogin(), true)
            : (await ExecutePasswordLogin(), false);

        if (result is null)
        {
            return;
        }

        DisplayExternalLoginErrorIfWeLandedHereFromExternalLogin();

        if (result.Succeeded)
        {
            var user = await UserManager.GetUserAsync(HttpContext.User);
            if (user is null)
            {
                NavigationService.RedirectTo(IdentityRoutes.LoginRoute);
                return;
            }

            LogUserLoggedIn(Logger);

            if (!isPasskeySignIn && UserPasswordIsExpired(user))
            {
                var username = user.UserName;
                await SignInManager.SignOutAsync();

                NavigationService.RedirectTo(
                    IdentityRoutes.ChangePasswordRoute,
                    new Dictionary<string, object?>()
                    {
                        { "user", username },
                        { "isPersistent", Input.RememberMe }
                    });
                return;
            }

            NavigationService.RedirectTo(ReturnUrl);

            return;
        }

        if (result.IsNotAllowed)
        {
            LogUserNotYetVerified(Logger);
            if (isPasskeySignIn)
            {
                messageStore.Add(() => Input.Username, Login.PasskeyLoginFailed);

                NavigationService.RedirectTo(IdentityRoutes.LoginRoute);
                return;
            }

            var user = await UserManager.FindByNameAsync(Input.Username);
            if (user is null)
            {
                NavigationService.RedirectTo(IdentityRoutes.LoginRoute);
                return;
            }

            if (!await IsAccountVerificationNeeded(user.Id))
                return;

            var code = await UserManager.GenerateEmailConfirmationTokenAsync(user);
            await SendVerificationEmail(user, code);

            NavigationService.RedirectTo(IdentityRoutes.EmailConfirmationRoute);

            return;
        }

        if (isPasskeySignIn)
        {
            messageStore.Add(() => Input.Username, Login.PasskeyLoginFailed);
            return;
        }

        if (result.IsLockedOut)
        {
            // Log for ops but surface the same generic error as a wrong password to avoid
            // disclosing account existence/state to attackers.
            LogUserLockedOut(Logger);
        }

        messageStore.Add(() => Input.Username, Login.UserOrPasswordIsIncorrect);
        messageStore.Add(() => Input.Password, Login.UserOrPasswordIsIncorrect);
    }

    private async Task<SignInResult?> ExecutePasswordLogin()
    {
        if (!editContext.Validate())
        {
            return null;
        }

        return await SignInManager.PasswordSignInAsync(Input.Username,
            Input.Password,
            Input.RememberMe,
            lockoutOnFailure: true);
    }

    private async Task<SignInResult?> ExecutePasskeyLogin()
    {
        // When performing passkey sign-in, don't perform form validation.

        if (!string.IsNullOrEmpty(Input.Passkey?.Error))
        {
            messageStore.Add(() => Input.Passkey, Input.Passkey.Error);
            return null;
        }

        return await SignInManager.PasskeySignInAsync(Input.Passkey?.CredentialJson ?? string.Empty);
    }

    private static bool UserPasswordIsExpired(SuiteUser? user) =>
        user?.PasswordExpirationDate <= DateTimeOffset.UtcNow;

    private void DisplayExternalLoginErrorIfWeLandedHereFromExternalLogin()
    {
        if (ExternalErrorTyped > ExternalLoginError.None)
            messageStore.Add(() => Input.Username, MapToErrorMessage(ExternalErrorTyped));
    }

    private static bool IsPasskeySignIn([NotNullWhen(true)] LoginFormModel? inputPasskey) =>
        !string.IsNullOrEmpty(inputPasskey?.Passkey?.CredentialJson);

    private string MapToErrorMessage(ExternalLoginError externalError)
        => externalError switch
        {
            ExternalLoginError.LoginFailed => Login.ExternalLoginFailed,
            ExternalLoginError.NoLocalUser => Login.NoLocalUser,
            ExternalLoginError.UnknownExternalUser => Login.UnknownExternalUser,
            ExternalLoginError.NewUserCreationFailed => Login.ExternalUserCreationFailed,
            ExternalLoginError.ExternalAccountAlreadyAssociated => Login.ExternalLoginAlreadyInUse,
            ExternalLoginError.None => string.Empty,
            _ => LogMissingErrorMessageMapping(externalError)
        };

    private string LogMissingErrorMessageMapping(ExternalLoginError externalError)
    {
        Logger.LogWarning("Missing error message mapping for ExternalLoginError: {Error}", externalError);
        return Login.ExternalLoginFailed;
    }

    private async Task InitializeExternalIdProvider()
    {
        IsExternalIdProviderConfigured
            = await ExternalAuthenticationSettings.IsExternalAuthenticationProviderConfigured();

        if (!IsExternalIdProviderConfigured)
            return;

        _externalProviderDisplayName = ProviderConstants.DefaultProviderDisplayName;
        _externalProviderName = ProviderConstants.DefaultProviderName;
    }

    private Task<bool> IsAccountVerificationNeeded(string userId)
        => AccountVerification
            .NeedsVerification(userId);

    private Task SendVerificationEmail(SuiteUser user, string code)
        => Mediator.Send(new SendVerifyEmailAddressLink(user.Id,
            NavigationService.CreateCallbackLink(IdentityRoutes.ConfirmMailRoute, user.Id, code)));

    [LoggerMessage(Level = LogLevel.Information, Message = "User logged in")]
    private static partial void LogUserLoggedIn(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "User logged in but is not yet verified")]
    private static partial void LogUserNotYetVerified(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "User account locked out")]
    private static partial void LogUserLockedOut(ILogger logger);
}
