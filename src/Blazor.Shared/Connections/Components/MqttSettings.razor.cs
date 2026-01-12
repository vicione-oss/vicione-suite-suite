using Blazor.Shared.Connections.Factories;
using Sdk.Client.Connections;
using Sdk.Connections.Contracts;
using ViciOne.Ui.Blazor.Components.ComboBox;

namespace Blazor.Shared.Connections.Components;

public sealed partial class MqttSettings : ConnectionSettingsComponentBase<MqttConnection>
{
    private readonly List<ComboBoxItem<MqttConnectionType, string>> _connectionTypes = ComboBoxItemFactory.GetMqttConnectionTypes();
}
