using System.ComponentModel;
using System.Runtime.CompilerServices;
using Blazor.Shared.Extensions;
using Core.Shared.Instance.Events;
using Microsoft.AspNetCore.Components;
using Sdk.Client.Infrastructure;
using Sdk.Client.Services;
using Sdk.Instance;
using ViciOne.SourceGenerators.NotifyChanged;

namespace Blazor.Shared.Services;

public sealed partial class LayoutService : ILayoutService, IEventConsumer<InstanceInformationUpdated>, IDisposable
{
    private readonly IInstanceInformationProvider _informationProvider;
    private readonly IDisposable _subscription;

    [NotifyChanged]
    public partial bool IsSidebarOpen { get; set; }

    [NotifyChanged]
    public partial bool IsLoadingOverlayVisible { get; set; }

    public Guid SelectedNotificationItem { get; set; }

    [NotifyChanged]
    public partial MarkupString InstanceTitle { get; set; }

    [NotifyChanged]
    public partial string? InstanceName { get; set; }

    [NotifyChanged]
    public partial string? TitleBarText { get; set; }

    [NotifyChanged]
    public partial string? TitleBarAppName { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public LayoutService(IInstanceInformationProvider informationProvider, IUiMediator uiMediator)
    {
        _informationProvider = informationProvider;
        _subscription = uiMediator.Register(this);
        InstanceTitle = _informationProvider.GetFormatedInstanceTitle();
        InstanceName = informationProvider.Local.Name;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public Task Consume(ClientContext<InstanceInformationUpdated> context,
        CancellationToken cancellationToken)
    {
        if (context.Message.InstanceInformation.Id == _informationProvider.Local.Id)
        {
            InstanceTitle = _informationProvider.GetFormatedInstanceTitle();
            InstanceName = _informationProvider.Local.Name;
        }

        return Task.CompletedTask;
    }

    public void Dispose() => _subscription.Dispose();
}
