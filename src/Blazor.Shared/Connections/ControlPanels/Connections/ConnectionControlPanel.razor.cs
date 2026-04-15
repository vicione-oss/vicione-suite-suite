using System.Globalization;
using Blazor.Shared.Connections.Contracts;
using Blazor.Shared.Connections.ControlPanels.Connections.Services;
using Blazor.Shared.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Client.Connections;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.Infrastructure;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using Sdk.Messaging;
using ViciOne.Ui.Blazor.Components.ComboBox;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Connections.ControlPanels.Connections;

[ControlPanelCategory<ControlPanelSystemCategoryDescriptor>]
public sealed partial class ConnectionControlPanel(IUiMediator mediator, IConnectionTypeRegistry connectionTypeRegistry, IConnectionTypeUiRegistry connectionTypeUiRegistry) : ControlPanelBase<ConnectionControlPanelState>, IEventConsumer<ConnectionChanged>
{
    private readonly string _descriptionBannerIconCssClass = MonochromeIconName.Connectivity.GetCssClasses().ToSpaceSeparated();
    private readonly List<ComboBoxItem<ConnectionType, string>> _connectionTypes = connectionTypeRegistry.GetConnectionTypes().Select(e => new ComboBoxItem<ConnectionType, string>() { Text = connectionTypeUiRegistry.TryGetDisplayName(e, out var displayName) ? displayName : e, Value = new ConnectionType(e) }).ToList();
    private readonly Dictionary<string, object> _connectionSettingsComponentParameters = [];
    private IDisposable? _subscriptionHandle;

    protected override void OnInitialized()
    {
        base.OnInitialized();

        _subscriptionHandle = mediator.Register(this);

        _connectionSettingsComponentParameters.Add(nameof(ConnectionSettingsComponentBase<>.Changed),
            EventCallback.Factory.Create(this, ConnectionSettingsChanged));
    }

    private async Task ConnectionSettingsChanged()
    {
        await BeginEdit();

        if (State.EditConnectionModel is null)
            return;

        var canBeTested = State.EditConnectionModel.CanBeTested;

        State.EditConnectionModel.TypedConnectionChanged();

        if (canBeTested != State.EditConnectionModel.CanBeTested)
            await InvokeAsync(StateHasChanged);
    }

    private async Task TagsChanged(IEnumerable<string> tags)
    {
        if (State.IsLoading || State.EditConnectionModel is null)
            return;

        // change callback from DxTagBox
        State.EditModelTagTexts = tags;
        var check = State.EditModelTagTexts.ToList();

        // simple change check
        if (State.EditConnectionModel.Tags.Count != check.Count)
        {
            await BeginEdit();
            return;
        }

        foreach (var tag in check)
        {
            if (State.EditConnectionModel.Tags.Any(k => k.Text == tag))
                continue;

            await BeginEdit();
            break;
        }
    }

    private string GetTitleOfConnectionDetails()
    {
        if (State.EditConnectionModel is null ||
            !State.ConnectionId.HasValue ||
            !connectionTypeUiRegistry.TryGetDisplayName(State.EditConnectionModel.Type, out var displayName) ||
            string.IsNullOrEmpty(displayName))
        {
            return Localization.ConnectionControlPanel.ConnectionDetails;
        }

        return string.Format(CultureInfo.CurrentCulture, Localization.ConnectionControlPanel.ConnectionDetailsForSpecificConnection, displayName);
    }

    public async Task Consume(ClientContext<ConnectionChanged> context, CancellationToken cancellationToken)
    {
        if (context.Message.Connection.Id != State.EditConnectionModel?.Connection.Id ||
            context.Message.ErrorInfo is not null)
        {
            return;
        }

        if (context.Message.Action is CrudAction.Created or CrudAction.Updated)
        {
            State.ConnectionId = context.Message.Connection.Id;
            State.EditConnectionModel = new EditConnectionModel(context.Message.Connection, connectionTypeRegistry);
            await InvokeAsync(StateHasChanged);
        }
    }

    protected override ValueTask DisposeAsyncCore()
    {
        _subscriptionHandle?.Dispose();

        return base.DisposeAsyncCore();
    }
}
