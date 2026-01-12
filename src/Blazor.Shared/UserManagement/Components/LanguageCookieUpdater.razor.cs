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

        var user = await AuthenticationStateProvider.GetUser();
        var userName = user?.Identity?.Name;

        var userLanguage = (await UiMediator.Request<GetUsers, GetUsersResponse>(
            new GetUsers(new(userName)))).Users.First().Language;

        var result = await UiMediator.Request<GetCrossInstanceConfiguration, GetCrossInstanceConfigurationResponse>(
            new GetCrossInstanceConfiguration());

        _defaultCulture = result.CrossInstanceConfiguration.CultureName;

        if (!string.IsNullOrEmpty(userName))
        {
            if (!string.IsNullOrEmpty(userLanguage))
            {
                var languageCookieValue = LanguageCookieReader.GetCookieValue();

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
                var languageCookieValue = LanguageCookieReader.GetCookieValue();

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

    private void UpdateCookie(string language, Guid nonceValue)
        => NavigationManager.NavigateTo($"{Constants.UpdateLanguageCookieRoute}/Update?language={language}&nonce={nonceValue}", true);

    private void RemoveCookie(Guid nonceValue)
        => NavigationManager.NavigateTo($"{Constants.UpdateLanguageCookieRoute}/Remove?nonce={nonceValue}", true);

    [LoggerMessage(1, LogLevel.Debug, @"Language cookie missing, trying to create language cookie with
        value '{Language}' for '{UserName}' and doing a redirect")]
    private static partial void LanguageCookieMissing(ILogger logger, string UserName, string Language);

    [LoggerMessage(2, LogLevel.Debug, @"Language cookie has value {LanguageCookieValue} but {UserLanguage} was expected,
        trying to update language cookie for '{UserName}' and doing a redirect")]
    private static partial void LanguageCookieUnexpectedValue(ILogger logger, string LanguageCookieValue, string UserLanguage,
        string UserName);

    [LoggerMessage(3, LogLevel.Debug, @"Language cookie has value {LanguageCookieValue} but default language {DefaultLanguage}
        should be used, trying to remove language cookie for '{UserName}' and doing a redirect")]
    private static partial void LanguageCookieExistsAndShouldBeRemoved(ILogger logger, string LanguageCookieValue, string DefaultLanguage,
        string UserName);
}
