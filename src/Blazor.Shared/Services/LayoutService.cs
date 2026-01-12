using System.ComponentModel;
using System.Runtime.CompilerServices;
using Core.Shared.Instance.Events;
using Microsoft.AspNetCore.Components;
using Sdk.Client.Infrastructure;
using Sdk.Client.Services;
using Sdk.Instance;

namespace Blazor.Shared.Services;

public sealed class LayoutService : ILayoutService, IEventConsumer<InstanceInformationUpdated>, IDisposable
{
    private readonly IInstanceInformationProvider _informationProvider;
    private string? _currentTitleBarAppName;
    private bool _isSidebarOpen;
    private bool _isLoadingOverlayVisible;
    private string? _titleBarText;
    private string? _instanceName;
    private MarkupString _instanceTitle;
    private readonly IDisposable _subscription;

    public bool IsSidebarOpen
    {
        get => _isSidebarOpen;
        set
        {
            if (value == _isSidebarOpen)
                return;

            _isSidebarOpen = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoadingOverlayVisible
    {
        get => _isLoadingOverlayVisible;
        set
        {
            if (value == _isLoadingOverlayVisible)
                return;

            _isLoadingOverlayVisible = value;
            OnPropertyChanged();
        }
    }
    public Guid SelectedNotificationItem { get; set; }

    public MarkupString InstanceTitle
    {
        get => _instanceTitle;
        set
        {
            if (string.Equals(_instanceTitle.Value, value.Value, StringComparison.Ordinal))
                return;

            _instanceTitle = value;
            OnPropertyChanged();
        }
    }

    public string? InstanceName
    {
        get => _instanceName;
        set
        {
            if (string.Equals(_instanceName, value, StringComparison.Ordinal))
                return;

            _instanceName = value;
            OnPropertyChanged();
        }
    }

    public string? TitleBarText
    {
        get => _titleBarText;
        set
        {
            if (string.Equals(_titleBarText, value, StringComparison.Ordinal))
                return;

            _titleBarText = value;
            OnPropertyChanged();
        }
    }

    public string? TitleBarAppName
    {
        get => _currentTitleBarAppName;
        set
        {
            if (string.Equals(_currentTitleBarAppName, value, StringComparison.Ordinal))
                return;

            _currentTitleBarAppName = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public LayoutService(IInstanceInformationProvider informationProvider, IUiMediator uiMediator)
    {
        _informationProvider = informationProvider;
        _subscription = uiMediator.Register(this);
        _instanceTitle = InitInstanceTitle(_informationProvider);
        _instanceName = informationProvider.Local.Name;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static MarkupString InitInstanceTitle(IInstanceInformationProvider informationProvider)
    {
        var formattedTitle = informationProvider.Local.FormattedName;
        return new MarkupString(formattedTitle
            .Replace("{", "<span class=\"text-accent\">", StringComparison.Ordinal)
            .Replace("}", "</span>", StringComparison.Ordinal));
    }

    public Task Consume(ClientContext<InstanceInformationUpdated> context,
        CancellationToken cancellationToken)
    {
        if (context.Message.InstanceInformation.Id == _informationProvider.Local.Id)
        {
            InstanceTitle = InitInstanceTitle(_informationProvider);
            InstanceName = _informationProvider.Local.Name;
        }

        return Task.CompletedTask;
    }

    public void Dispose() => _subscription.Dispose();
}
