using Blazor.Shared.Authorization.Extensions;
using Blazor.Shared.UserManagement.Services;
using Core.Shared.Instance.Requests;
using Core.Shared.Instance.Services;
using Core.Shared.UserManagement.Events;
using Core.Shared.UserManagement.Extensions;
using Core.Shared.UserManagement.Requests;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.UserManagement.Components;

public sealed partial class LanguageCookieUpdater : ComponentBase, IDisposable, IEventConsumer<UserUpdatedEvent>
{
    private IDisposable? _userUpdatedEventRegistration;
    private bool _disposedAsync;
    private string _defaultCulture = string.Empty;

    [Inject] public required AuthenticationStateProvider AuthenticationStateProvider { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }
    [Inject] public required ILanguageCookieReader LanguageCookieReader { get; set; }
    [Inject] public required IUiMediator UiMediator { get; set; }
    [Inject] public required INonceStore NonceStore { get; set; }
    [Inject] public required ILogger<LanguageCookieUpdater> Logger { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        var userName = await AuthenticationStateProvider.GetUserName();

        // The user may not be present on this instance (an anonymous request, or a slave whose
        // replica does not have the account yet), so the result can be empty — fall back to no
        // user language instead of throwing. The cookie logic below already handles a null value.
        var userLanguage = (await UiMediator.Request<GetUsers, GetUsersResponse>(
            new GetUsers(new(userName)))).Users.FirstOrDefault()?.Language;

        var result = await UiMediator.Request<GetCrossInstanceConfiguration, GetCrossInstanceConfigurationResponse>(
            new GetCrossInstanceConfiguration());

        _defaultCulture = result.CrossInstanceConfiguration.CultureName;

        if (!string.IsNullOrEmpty(userName))
        {
            var languageCookieValue = LanguageCookieReader.GetCookieValue();

            if (!string.IsNullOrEmpty(userLanguage))
            {
                if (string.IsNullOrEmpty(languageCookieValue))
                {
                    LanguageCookieMissing(Logger, userName, userLanguage);

                    var nonce = await NonceStore.Create(CancellationToken.None);

                    UpdateCookie(userLanguage, nonce.Value);
                    return;
                }
                else if (!languageCookieValue.Contains(userLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    LanguageCookieUnexpectedValue(Logger, languageCookieValue, userLanguage, userName);

                    var nonce = await NonceStore.Create(CancellationToken.None);

                    UpdateCookie(userLanguage, nonce.Value);
                    return;
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(languageCookieValue))
                {
                    LanguageCookieExistsAndShouldBeRemoved(Logger, languageCookieValue, _defaultCulture, userName);

                    var nonce = await NonceStore.Create(CancellationToken.None);

                    RemoveCookie(nonce.Value);
                    return;
                }
            }
        }

        _userUpdatedEventRegistration = UiMediator.Register(this);
    }

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposedAsync, true, false))
            return;

        _userUpdatedEventRegistration?.Dispose();
    }

    public async Task Consume(ClientContext<UserUpdatedEvent> context, CancellationToken cancellationToken)
    {
        try
        {
            var user = await AuthenticationStateProvider.GetUser();
            var userProfile = context.Message.UserProfile;

            if (!user.IsAssociatedWith(userProfile))
                return;

            if (!userProfile.IsLanguageChanged(context.Message.UserProfileBefore))
                return;

            var nonce = await NonceStore.Create(cancellationToken);

            if (userProfile.Language is null)
                RemoveCookie(nonce.Value);
            else
                UpdateCookie(userProfile.Language ?? _defaultCulture, nonce.Value);
        }
        catch (Exception ex)
        {
            LogConsumeUserUpdateFailed(Logger, ex);
        }
    }

    /// <summary>
    /// Will request backend controller to append language cookie to http header and redirect to ~/ afterwards
    /// </summary>    
    private void UpdateCookie(string language, Guid nonceValue)
        => NavigationManager.NavigateTo($"{Constants.UpdateLanguageCookieRoute}/Update?language={language}&nonce={nonceValue}", true);

    /// <summary>
    /// Will request backend controller to remove language cookie from http header and redirect to ~/ afterwards
    /// </summary>    
    private void RemoveCookie(Guid nonceValue)
        => NavigationManager.NavigateTo($"{Constants.UpdateLanguageCookieRoute}/Remove?nonce={nonceValue}", true);

    [LoggerMessage(LogLevel.Debug, @"Language cookie missing, trying to create language cookie with
        value '{Language}' for '{UserName}' and doing a redirect")]
    private static partial void LanguageCookieMissing(ILogger logger, string UserName, string Language);

    [LoggerMessage(LogLevel.Debug, @"Language cookie has value {LanguageCookieValue} but {UserLanguage} was expected,
        trying to update language cookie for '{UserName}' and doing a redirect")]
    private static partial void LanguageCookieUnexpectedValue(ILogger logger, string LanguageCookieValue, string UserLanguage,
        string UserName);

    [LoggerMessage(LogLevel.Debug, @"Language cookie has value {LanguageCookieValue} but default language {DefaultLanguage}
        should be used, trying to remove language cookie for '{UserName}' and doing a redirect")]
    private static partial void LanguageCookieExistsAndShouldBeRemoved(ILogger logger, string LanguageCookieValue, string DefaultLanguage,
        string UserName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed consume use update change")]
    private static partial void LogConsumeUserUpdateFailed(ILogger logger, Exception exception);
}
