using System.Globalization;
using Blazor.Shared.Services;
using Core.Shared.HostManagement.Commands;
using Core.Shared.HostManagement.Events;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Sdk.Authorization;
using Sdk.Client.Infrastructure;
using Sdk.Client.NotificationArea.Components;
using Sdk.Utils;

namespace Blazor.Shared.SystemInformation.NotificationArea;

public sealed partial class SystemInformationNotificationElementFlyoutContent : INotificationElementFlyoutContent,
    IEventConsumer<ControlSystemCompleted>,
    IDisposable
{
    private AuthenticationState? _authState;
    private bool _restartSystemDialogVisible;
    private bool _restartDialogVisible;
    private bool _shutdownDialogVisible;
    private readonly AutoDisposeList<IDisposable> _subscriptionHandles = [];

    private string? SysAdminPolicy { get; }
        = ModulePolicyProvider.GetPolicy(Core.Shared.Constants.SystemModuleId, AccessLevel.Full);

    [Inject]
    private ILogger<SystemInformationNotificationElementFlyoutContent> Logger { get; set; } = default!;

    [Inject]
    private ISuiteControlService SuiteControlService { get; set; } = default!;

    [Inject]
    private IUiMediator Mediator { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    private static string TwentyFourHoursLabel => string.Format(CultureInfo.InvariantCulture, ViciOne.Ui.Localization.Resources.CommonPatterns.MultipleHours, 24);
    private static string OneHourLabel => string.Format(CultureInfo.InvariantCulture, ViciOne.Ui.Localization.Resources.CommonPatterns.SingleHour, 1);

    protected override async Task OnInitializedAsync()
    {
        _subscriptionHandles.Add(Mediator.Register(this));

        // ToDo - Setzen
        if (AuthenticationStateTask is not null)
            _authState = await AuthenticationStateTask;
    }

    private void OpenMonitor()
        => NavigationManager.NavigateTo(Constants.ProcessRoute);

    private async Task RestartSuite()
    {
        await SuiteControlService.RestartSuite();
        _restartDialogVisible = false;
    }

    private async Task RestartSystem()
    {
        await SuiteControlService.RestartSystem();
        _restartSystemDialogVisible = false;
    }

    private async Task ShutdownSystem()
    {
        await SuiteControlService.ShutdownSystem();
        _shutdownDialogVisible = false;
    }

    public Task Consume(ClientContext<ControlSystemCompleted> context, CancellationToken cancellationToken)
    {
        if (context.Message.ErrorInfo is not null)
        {
            LogControlSystemFailed(Logger, context.Message.Command, context.Message.ErrorInfo.ErrorCode, context.Message.ErrorInfo.Message);
            return Task.CompletedTask;
        }

        LogControlSystemCompleted(Logger, context.Message.Command);

        return Task.CompletedTask;
    }

    [LoggerMessage(1, LogLevel.Information, "{Command} system completed")]
    private static partial void LogControlSystemCompleted(ILogger logger, SystemCommand command);

    [LoggerMessage(2, LogLevel.Error, "{Command} system failed with error code {ErrorCode} and message {Message}")]
    private static partial void LogControlSystemFailed(ILogger logger, SystemCommand command, int ErrorCode, string? Message);

    public void Dispose()
        => _subscriptionHandles.Dispose();
}
