using Blazor.Shared.Connections.Services;
using Blazor.Shared.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Client.Connections;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Connections.Contracts;
using ViciOne.Ui.Blazor.Components.ComboBox;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Connections.ControlPanels;

[ControlPanelCategory<ControlPanelSystemCategoryDescriptor>]
public sealed partial class ConnectionControlPanel(IConnectionTypeRegistry connectionTypeRegistry, IConnectionTypeUiRegistry connectionTypeUiRegistry) : ControlPanelBase<ConnectionControlPanelState>
{
    private readonly string _descriptionBannerIconCssClass = MonochromeIconName.Connectivity.GetCssClasses().ToSpaceSeparated();
    private readonly List<ComboBoxItem<ConnectionType, string>> _connectionTypes = connectionTypeRegistry.GetConnectionTypes().Select(e => new ComboBoxItem<ConnectionType, string>() { Text = connectionTypeUiRegistry.TryGetDisplayName(e, out var displayName) ? displayName : e, Value = new ConnectionType(e) }).ToList();
    private readonly Dictionary<string, object> _connectionSettingsComponentParameters = [];

    protected override void OnInitialized()
    {
        base.OnInitialized();

        _connectionSettingsComponentParameters.Add(nameof(ConnectionSettingsComponentBase<MqttConnection>.Changed),
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
}
