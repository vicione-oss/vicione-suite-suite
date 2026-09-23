using Blazor.Shared.Connections.Factories;
using Sdk.Client.Connections;
using Sdk.Connections.Contracts;
using ViciOne.Ui.Blazor.Components.ComboBox;

namespace Blazor.Shared.Connections.Components;

public sealed partial class MqttSettings : ConnectionSettingsComponentBase<MqttConnection>
{
    private readonly List<ComboBoxItem<MqttConnectionType, string>> _connectionTypes = ComboBoxItemFactory.GetMqttConnectionTypes();

    private readonly List<ComboBoxItem<MqttProtocolVersion, string>> _protocolVersions = ComboBoxItemFactory.GetMqttProtocolVersions();

    private readonly List<ComboBoxItem<MqttSslProtocol?, string>> _supportedSslProtocols = ComboBoxItemFactory.GetSupportedMqttSslProtocols();

    private readonly List<ComboBoxItem<MqttQualityOfServiceLevel, string>> _qualityOfServiceLevels = ComboBoxItemFactory.GetMqttQualityOfServiceLevels();

    private bool _isSslEnabled;
    private bool _isPortReadonly;
    private bool _showWillFields;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            // OnParametersSet is unusable because the control is already rendered
            // as dynamic component and the parameters are set after each property change
            var showWillFields = !string.IsNullOrWhiteSpace(Connection.WillTopic)
                || !string.IsNullOrWhiteSpace(Connection.WillMessage);

            if (_showWillFields != showWillFields)
            {
                _showWillFields = showWillFields;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        UpdateFields();
    }

    private bool UpdateFields()
    {
        var sslEnabled = Connection.SslProtocol is not null;
        var portReadonly = Connection.Protocol == MqttConnectionType.WebSocket;
        var hasChanges = false;

        if (_isSslEnabled != sslEnabled)
        {
            _isSslEnabled = sslEnabled;
            hasChanges = true;
        }

        if (_isPortReadonly != portReadonly)
        {
            _isPortReadonly = portReadonly;
            hasChanges = true;
        }

        return hasChanges;
    }

    private async Task UpdateFieldsAndNotifyChanged()
    {
        if (UpdateFields())
            await NotifyChanged();
    }

    private Task NotifyWillChanged()
    {
        if (!_showWillFields)
        {
            // Disabling clears the related fields.
            Connection.WillMessage = string.Empty;
            Connection.WillTopic = string.Empty;
            Connection.WillRetain = false;
            Connection.QualityOfService = MqttQualityOfServiceLevel.AtMostOnce;
            return Task.CompletedTask;
        }

        return NotifyChanged();
    }
}
