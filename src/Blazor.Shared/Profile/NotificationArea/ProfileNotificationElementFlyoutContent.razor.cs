using System.Globalization;
using Blazor.Shared.Profile.Services.TicketCenter;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.DateAndTime.Services;
using Blazor.Shared.UserManagement.Services;
using Blazor.Shared.Validation.Services.Validators;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Requests;
using Core.Shared.Security;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Mappers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using Sdk.Client.NotificationArea.Components;
using ViciOne.Ui.Blazor.Components.ComboBox;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Profile.NotificationArea;

public sealed partial class ProfileNotificationElementFlyoutContent(
    IExternalAuthenticationSettings externalAuthenticationSettings) : ComponentBase, INotificationElementFlyoutContent
{
    private UserProfile? _userProfile;
    private UserProfile? _userProfileBaseline;
    private bool _editUserDataStarted;
    //private bool _absence = true;
    //private bool _notifications = true;
    private readonly UserProfileMapper _userProfileMapper = new();
    private CrossInstanceConfiguration? _crossInstanceConfiguration;
    private List<UserProfile> _users = [];
    private bool _canEditUserData;
    private string? _errorMessage;
    private List<ComboBoxItem<CultureInfo?, string>> _availableCultures = [];
    private CultureInfo? _selectedCulture;
    private bool _cultureChanged;
    private List<ComboBoxItem<string?, string>> _availableTimeZones = [];
    private bool _availableTimeZonesLoading = true;
    private string? _selectedTimeZone;
    private ExternalUserAccount? _externalAccount;

    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private INavigationService Navigation { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IUrgentProvider TicketCenterUrgentProvider { get; set; } = default!;
    [Inject] private ITodayProvider TicketCenterTodayProvider { get; set; } = default!;
    [Inject] private IThisWeekProvider TicketCenterThisWeekProvider { get; set; } = default!;
    [Inject] private ISoonProvider TicketCenterSoonProvider { get; set; } = default!;
    [Inject] private IUserService UserService { get; set; } = default!;
    [Inject] private IMemoryCache MemoryCache { get; set; } = default!;
    [Inject] private IUiMediator Mediator { get; set; } = default!;
    [Inject] private ITimeZoneDescriptorProvider TimeZoneDescriptorProvider { get; set; } = default!;
    [Inject] private IEmailValidator EmailValidator { get; set; } = default!;
    [Inject] private IPhoneNumberValidator PhoneNumberValidator { get; set; } = default!;
    [Inject] private ILogger<ProfileNotificationElementFlyoutContent> Logger { get; set; } = default!;
    [Inject] public IExternalAccountService ExternalAccountService { get; set; } = default!;
    private bool ShowAddExternalLogin
        => IsExternalAccountProviderConfigured && _externalAccount is null;
    private bool IsExternalAccountProviderConfigured { get; set; }

    private Task BeginSignOut() => Navigation.Logout();

    protected override async Task OnInitializedAsync()
    {
        var result = await Mediator.Request<GetCrossInstanceConfiguration, GetCrossInstanceConfigurationResponse>(
            new GetCrossInstanceConfiguration());

        _crossInstanceConfiguration = result.CrossInstanceConfiguration;

        var emptyCultureEntry = new ComboBoxItem<CultureInfo?, string>
        {
            Text = $"{CommonVocabulary.Default} - {new CultureInfo(_crossInstanceConfiguration.CultureName).DisplayName}",
            Value = null
        };

        _availableCultures = [emptyCultureEntry];
        _availableCultures.AddRange(Constants.SupportedCultures.Select(c => new ComboBoxItem<CultureInfo?, string>
        {
            Text = c.DisplayName,
            Value = c
        }));

        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var identityName = authState.User.Identity?.Name;
        if (identityName is not null)
        {
            _users = await UserService.GetUsers(new UserName(identityName));
            if (_users.Count > 0)
            {
                _userProfile = _users[0];
                _externalAccount = await ExternalAccountService.GetExternalUserAccount(identityName);
            }
        }
        else
        {
            Logger.LogError("Unable to get profile for user, identity is null or no name provided");
        }

        _canEditUserData = true;

        IsExternalAccountProviderConfigured
            = await externalAuthenticationSettings.IsExternalAuthenticationProviderConfigured();
    }

    private async Task EditUserData()
    {
        if (Interlocked.CompareExchange(ref _editUserDataStarted, true, false))
            return;

        if (_userProfile is not null)
        {
            _userProfileBaseline = _userProfileMapper.Map(_userProfile);

            _selectedCulture = _availableCultures.FirstOrDefault(c => c.Value?.Name == _userProfile.Language)?.Value;
        }
        else
        {
            _userProfileBaseline = null;

            _selectedCulture = null;
        }

        if (_crossInstanceConfiguration is not null)
        {
            var defaultTimeZoneDescriptor = await TimeZoneDescriptorProvider.GetTimeZoneDescriptor(
                _crossInstanceConfiguration.TimeZoneId);

            _availableTimeZones =
            [
                new ComboBoxItem<string?, string>
                {
                    Text = $"{CommonVocabulary.Default} - {defaultTimeZoneDescriptor?.DisplayName}",
                    Value = null
                },
            ];

            var timeZoneDescriptors = await TimeZoneDescriptorProvider.GetAll();

            _availableTimeZones.AddRange(timeZoneDescriptors
                .OrderBy(Settings.DateAndTime.Constants.OrderByKeySelector)
                .Select(ts => new ComboBoxItem<string?, string> { Text = ts.DisplayName, Value = ts.TimeZoneId })
            );
        }

        if (_userProfile is not null)
            _selectedTimeZone = _availableTimeZones.FirstOrDefault(tz => tz.Value == _userProfile.TimeZone)?.Value;
        else
            _selectedTimeZone = null;

        _availableTimeZonesLoading = false;
    }

    private async Task ConfirmUserDataAsync()
    {
        if (_userProfile is null)
            return;

        if (!EmailValidator.Validate(_userProfile.Email, CommonVocabulary.Email, out var errorMessage) ||
            !PhoneNumberValidator.Validate(_userProfile.Mobile, CommonVocabulary.Cellphone, out errorMessage))
        {
            _errorMessage = errorMessage;

            return;
        }
        else
        {
            _errorMessage = null;
        }

        if (!Interlocked.CompareExchange(ref _editUserDataStarted, false, true))
            return;

        if (_cultureChanged)
            MemoryCache.Remove(Constants.GetUserCultureCacheKey(_userProfile.UserName.Value));

        await UserService.UpdateUser(_userProfile);

        // Do not implement anything after UpdateUser() as LanguageCookieUpdater is listening to UserUpdatedEvent
        // which results in an immediate redirect to UpdateAuthenticationCookieController, hence logic executed
        // here would either interfere with the redirect or would not get executed correctly.
    }

    private void CancelUserData()
    {
        if (!_editUserDataStarted)
            return;

        _errorMessage = null;

        if (_userProfileBaseline is not null)
            _userProfile = _userProfileMapper.Map(_userProfileBaseline);
        else
            _userProfile = null;

        _cultureChanged = false;
        _editUserDataStarted = false;
    }

    private void ChangeStateAbsence(bool value)
    {
        //_absence = value;
        // TODO - any action
    }

    private void ChangeStateNotification(bool value)
    {
        //_notifications = value;
        // TODO - any action
    }

    private void CultureChanged()
    {
        if (_userProfile is null)
            return;

        var language = _selectedCulture?.Name;

        if (_userProfile.Language == language)
            return;

        _userProfile.Language = language;
        _cultureChanged = true;
    }

    private void TimeZoneChanged()
    {
        if (_userProfile is null)
            return;

        if (_userProfile.TimeZone == _selectedTimeZone)
            return;

        _userProfile.TimeZone = _selectedTimeZone;
    }
}
