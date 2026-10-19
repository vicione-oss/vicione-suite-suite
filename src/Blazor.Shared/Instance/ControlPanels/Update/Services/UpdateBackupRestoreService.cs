using System.Globalization;
using Blazor.Shared.Services;
using Core.Shared;
using Core.Shared.Persistence.Commands;
using Core.Shared.Persistence.Events;
using Core.Shared.Persistence.Requests;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using Sdk.Client.Services;
using Sdk.MessageBanner.Contracts;
using Sdk.Messaging;
using Sdk.Utils;
using CapabilityTexts = Blazor.Shared.Localization.HostManagementCapabilities;

namespace Blazor.Shared.Instance.ControlPanels.Update.Services;

/// <summary>
/// Runs the backup export and restore of <see cref="UpdateBackupRestoreControlPanel"/>, so that they complete while another
/// tab is shown and the control panel component is disposed.
/// </summary>
internal sealed partial class UpdateBackupRestoreService : IEventConsumer<RestoreBackupPrepared>, IEventConsumer<BackupFinished>,
    IDisposable
{
    private readonly IUiMediator _mediator;
    private readonly IMessageBannerService _bannerService;
    private readonly IJsInterop _jsInterop;
    private readonly ILogger<UpdateBackupRestoreService> _logger;
    private readonly AutoDisposeList<IDisposable> _subscriptionHandle = [];

    private Guid? _exportCorrelationId;

    public bool IsExportInProgress { get; private set; }

    public bool IsRestoreInProgress { get; private set; }

    public string? ErrorMessage { get; private set; }

    public event Action? Changed;

    public UpdateBackupRestoreService(IUiMediator mediator,
        IMessageBannerService bannerService,
        IJsInterop jsInterop,
        ILogger<UpdateBackupRestoreService> logger)
    {
        _mediator = mediator;
        _bannerService = bannerService;
        _jsInterop = jsInterop;
        _logger = logger;

        _subscriptionHandle.Add(mediator.Register<RestoreBackupPrepared>(this));
        _subscriptionHandle.Add(mediator.Register<BackupFinished>(this));
    }

    public async Task StartExport()
    {
        try
        {
            ErrorMessage = null;
            IsExportInProgress = true;

            // The correlated BackupFinished starts the download.
            var command = new CreateBackup();
            _exportCorrelationId = command.CorrelationId;
            await _mediator.Send(command);
        }
        catch (Exception ex)
        {
            CreateBackupFailed(_logger, ex);
            ErrorMessage = ex.Message;
            _exportCorrelationId = null;
            IsExportInProgress = false;
        }
    }

    public void ClearError()
        => ErrorMessage = null;

    public async Task StartRestore(string backupFilePath)
    {
        try
        {
            IsRestoreInProgress = true;

            // The UI offers no choice between the system and the suite configuration yet, so both are restored.
            var command = new RestoreBackup
            {
                BackupFilePath = backupFilePath,
                SuiteConfiguration = true,
                SystemConfiguration = true
            };

            await _mediator.Send(command);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            IsRestoreInProgress = false;
            RestoreBackupFailed(_logger, ex);
        }
    }

    public void EndRestore()
        => IsRestoreInProgress = false;

    public Task Consume(ClientContext<RestoreBackupPrepared> context, CancellationToken cancellationToken)
    {
        // Not correlated, because the suite restarts shortly after and that is relevant for every user.
        IsRestoreInProgress = false;

        if (context.Message.ErrorInfo is not null)
            _bannerService.ShowMessageBanner(MessageType.Error, GetRestoreErrorMessage(context.Message.ErrorInfo));
        else
            _bannerService.ShowMessageBanner(MessageType.Information, Localization.UpdateControlPanel.BackupRestoreInProgress);

        Changed?.Invoke();

        return Task.CompletedTask;
    }

    private static string? GetRestoreErrorMessage(ErrorInfo errorInfo)
        => errorInfo.ErrorCode switch
        {
            RestoreBackupPrepared.RestartServiceDisabled => CapabilityTexts.FunctionDisabled,
            RestoreBackupPrepared.SettingsDisabled => string.Format(CultureInfo.CurrentCulture, CapabilityTexts.SettingsDisabled, errorInfo.Message),
            RestoreBackupPrepared.BackupFormatNotSupported => Localization.UpdateControlPanel.BackupFormatNotSupported,
            _ => errorInfo.Message
        };

    public async Task Consume(ClientContext<BackupFinished> context, CancellationToken cancellationToken)
    {
        if (context.CorrelationId != _exportCorrelationId)
            return;

        try
        {
            if (context.Message.ErrorInfo is not null)
            {
                _bannerService.ShowMessageBanner(MessageType.Error, context.Message.ErrorInfo.Message);
                return;
            }

            var response = await _mediator.Request<GetBackup, GetBackupResponse>(new GetBackup(), cancellationToken);

            if (response.RequestError is not null)
                throw new InvalidOperationException(response.RequestError.Message);

            if (response.Content is null)
                throw new InvalidOperationException($"Content of {response.FileName} is null");

            using var memoryStream = new MemoryStream(response.Content);

            await _jsInterop.DownloadAs(memoryStream, response.FileName, cancellationToken);
        }
        catch (Exception e)
        {
            _bannerService.ShowMessageBanner(MessageType.Error, e.Message);
        }
        finally
        {
            _exportCorrelationId = null;
            IsExportInProgress = false;

            Changed?.Invoke();
        }
    }

    public void Dispose()
        => _subscriptionHandle.Dispose();

    [LoggerMessage(LogLevel.Error, "Failed to create backup")]
    private static partial void CreateBackupFailed(ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Failed to restore backup")]
    private static partial void RestoreBackupFailed(ILogger logger, Exception exception);
}
