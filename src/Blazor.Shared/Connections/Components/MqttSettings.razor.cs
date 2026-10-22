using Blazor.Shared.Connections.Factories;
using Sdk.Client.Connections;
using Sdk.Connections.Contracts;
using ViciOne.Ui.Blazor.Components.ComboBox;

namespace Blazor.Shared.Connections.Components;

public sealed partial class MqttSettings : ConnectionSettingsComponentBase<MqttConnection>
{
    private const int DefaultTcpPort = 1883;
    private const int DefaultWebSocketPort = 9001;

    private readonly List<ComboBoxItem<MqttConnectionType, string>> _connectionTypes = ComboBoxItemFactory.GetMqttConnectionTypes();

    private readonly List<ComboBoxItem<MqttProtocolVersion, string>> _protocolVersions = ComboBoxItemFactory.GetMqttProtocolVersions();

    private readonly List<ComboBoxItem<MqttSslProtocol?, string>> _supportedSslProtocols = ComboBoxItemFactory.GetSupportedMqttSslProtocols();

    private readonly List<ComboBoxItem<MqttQualityOfServiceLevel, string>> _qualityOfServiceLevels = ComboBoxItemFactory.GetMqttQualityOfServiceLevels();

    private bool _isSslEnabled;
    private bool _showWillFields;

    protected override void OnInitialized()
    {
        base.OnInitialized();

#pragma warning disable CS0618 // Connections stored before SslProtocol existed still carry TCPWithTLS.
        if (Connection.Protocol == MqttConnectionType.TCPWithTLS)
#pragma warning restore CS0618
        {
            // Shown in its current form without marking the panel as edited; the next save stores it.
            Connection.Protocol = MqttConnectionType.TCP;
            Connection.SslProtocol ??= MqttSslProtocol.Tls12;
        }
    }

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

    private void UpdateFields()
        => _isSslEnabled = Connection.SslProtocol is not null;

    private Task UpdateFieldsAndNotifyChanged()
    {
        UpdateFields();

        return NotifyChanged();
    }

    private Task ProtocolChanged()
    {
        // A port the user set stays; only the other transport's default follows the switch.
        Connection.Port = (Connection.Protocol, Connection.Port) switch
        {
            (MqttConnectionType.WebSocket, DefaultTcpPort) => DefaultWebSocketPort,
            (MqttConnectionType.TCP, DefaultWebSocketPort) => DefaultTcpPort,
            _ => Connection.Port,
        };

        return NotifyChanged();
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
        }

        return NotifyChanged();
    }
}
