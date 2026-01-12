using System.Globalization;
using Blazor.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Sdk.Authorization;
using Sdk.Client.NotificationArea.Components;

namespace Blazor.Shared.SystemInformation.NotificationArea;

public sealed partial class SystemInformationNotificationElementFlyoutContent : INotificationElementFlyoutContent
{
    private AuthenticationState? _authState;

    private string? SysAdminPolicy { get; }
        = ModulePolicyProvider.GetPolicy(Sdk.Constants.SystemModuleId, AccessLevel.Full);

    [Inject]
    private ILogger<SystemInformationNotificationElementFlyoutContent> Logger { get; set; } = default!;

    [Inject]
    private ISuiteControlService SuiteControlService { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    private string TwentyFourHoursLabel => string.Format(CultureInfo.InvariantCulture, ViciOne.Ui.Localization.Resources.CommonPatterns.MultipleHours, 24);
    private string OneHourLabel => string.Format(CultureInfo.InvariantCulture, ViciOne.Ui.Localization.Resources.CommonPatterns.SingleHour, 1);

    protected override async Task OnInitializedAsync()
    {
        // ToDo - Setzen
        if (AuthenticationStateTask is not null)
            _authState = await AuthenticationStateTask;
    }

    private void OpenMonitor()
        => NavigationManager.NavigateTo(Constants.ProcessRoute);

    private void BeginShutdown() => Logger.LogDebug("Shutdown the application...todo");
}
