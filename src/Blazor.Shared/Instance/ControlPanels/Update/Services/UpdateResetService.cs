using Blazor.Shared.Services;
using Core.Shared.HostManagement.Commands;
using Core.Shared.HostManagement.Events;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using Sdk.Client.Services;
using Sdk.Instance;
using Sdk.MessageBanner.Contracts;

namespace Blazor.Shared.Instance.ControlPanels.Update.Services;

/// <summary>
/// Runs the reset of <see cref="UpdateResetControlPanel"/>, so that it completes while another tab is shown and the control
/// panel component is disposed.
/// </summary>
internal sealed partial class UpdateResetService(IUiMediator mediator,
    IInstanceInformationProvider instanceInformation,
    IMessageBannerService bannerService,
    INavigationService navigationService,
    ILogger<UpdateResetService> logger) : IEventConsumer<ControlSystemCompleted>, IDisposable
{
    private IDisposable? _subscription;
    private Guid? _resetCorrelationId;

    public bool IsResetInProgress { get; private set; }

    public string? ErrorMessage { get; private set; }

    public event Action? Changed;

    public void ClearError()
        => ErrorMessage = null;

    /// <summary>
    /// Tells whether a <see cref="ControlSystemCompleted"/> answers the reset of this service, which handles it already.
    /// </summary>
    public bool IsOwnReset(Guid correlationId)
        => correlationId == _resetCorrelationId;

    public async Task StartReset()
    {
        try
        {
            ErrorMessage = null;
            IsResetInProgress = true;

            // Subscribed only for the reset, so that a completion of another command does not log the user out.
            _subscription ??= mediator.Register<ControlSystemCompleted>(this);

            var command = new ControlSystem(SystemCommand.Reset);
            _resetCorrelationId = command.CorrelationId;
            await mediator.Send(command, instanceInformation.Local.Id);
        }
        catch (Exception ex)
        {
            ControlSystemFailed(logger, ex);
            ErrorMessage = ex.Message;
            IsResetInProgress = false;
            _resetCorrelationId = null;
        }
    }

    public async Task Consume(ClientContext<ControlSystemCompleted> context, CancellationToken cancellationToken)
    {
        if (!IsOwnReset(context.Message.CorrelationId))
            return;

        IsResetInProgress = false;

        if (context.Message.ErrorInfo is not null)
        {
            _resetCorrelationId = null;
            ErrorMessage = context.Message.ErrorInfo.Message;
            bannerService.ShowMessageBanner(MessageType.Information, ErrorMessage);
            Changed?.Invoke();
            return;
        }

        ErrorMessage = null;
        bannerService.ShowMessageBanner(MessageType.Information, Localization.UpdateControlPanel.ResetInProgressMessage);
        Changed?.Invoke();

        // No delay, because the restart follows immediately and leaves no time to read the banner anyway.
        await navigationService.Logout();
    }

    public void Dispose()
        => _subscription?.Dispose();

    [LoggerMessage(LogLevel.Error, "Failed to control system")]
    private static partial void ControlSystemFailed(ILogger logger, Exception exception);
}
