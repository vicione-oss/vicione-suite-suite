using Sdk.Connections.Contracts;
using ViciOne.Ui.Blazor.Components.ComboBox;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Connections.Factories;

internal static class ComboBoxItemFactory
{
    /// <summary>
    /// According to https://gitlab.com/vicione-oss/vicione/runtime-libs/dp-mqtt/-/blob/main/src/Mqtt/MqttDataPortProperties.cs?ref_type=heads
    /// </summary>
    public static List<ComboBoxItem<MqttSslProtocol?, string>> GetSupportedMqttSslProtocols()
    {
        var noneItem = new ComboBoxItem<MqttSslProtocol?, string> { Value = null, Text = "None" };
        var enumItems = Enum.GetValues<MqttSslProtocol>()
            .Select(enumValue =>
            {
                var displayText = enumValue switch
                {
                    MqttSslProtocol.Tls12 => "Tls 1.2",
                    MqttSslProtocol.Tls13 => "Tls 1.3",
                    _ => enumValue.ToString(),
                };

                return new ComboBoxItem<MqttSslProtocol?, string> { Value = enumValue, Text = displayText };
            });

        var list = new List<ComboBoxItem<MqttSslProtocol?, string>> { noneItem };
        list.AddRange(enumItems);

        return list;
    }

    public static List<ComboBoxItem<MqttProtocolVersion, string>> GetMqttProtocolVersions()
        => Enum.GetValues<MqttProtocolVersion>()
            .Select(enumValue =>
            {
                var displayText = enumValue switch
                {
                    MqttProtocolVersion.V500 => "MQTT 5.0",
                    MqttProtocolVersion.V311 => "MQTT 3.1.1",
                    _ => enumValue.ToString(),
                };

                return new ComboBoxItem<MqttProtocolVersion, string> { Value = enumValue, Text = displayText };
            })
            .ToList();

    public static List<ComboBoxItem<MqttQualityOfServiceLevel, string>> GetMqttQualityOfServiceLevels()
        => Enum.GetValues<MqttQualityOfServiceLevel>()
            .Select(enumValue =>
            {
                var displayText = enumValue switch
                {
                    MqttQualityOfServiceLevel.AtLeastOnce => "AtLeastOnce",
                    MqttQualityOfServiceLevel.AtMostOnce => "AtMostOnce",
                    MqttQualityOfServiceLevel.ExactlyOnce => "ExactlyOnce",
                    _ => enumValue.ToString(),
                };

                return new ComboBoxItem<MqttQualityOfServiceLevel, string> { Value = enumValue, Text = displayText };
            })
            .ToList();

    public static List<ComboBoxItem<MqttConnectionType, string>> GetMqttConnectionTypes()
        => Enum.GetValues<MqttConnectionType>()
            .Where(enumValue => enumValue is MqttConnectionType.TCP or MqttConnectionType.WebSocket)
            .Select(enumValue =>
            {
                var displayText = enumValue switch
                {
                    MqttConnectionType.TCP => TechnicalAcronyms.Tcp,
                    MqttConnectionType.WebSocket => TechnicalTerms.WebSocket,
                    _ => enumValue.ToString(),
                };

                return new ComboBoxItem<MqttConnectionType, string>()
                {
                    Value = enumValue,
                    Text = displayText,
                };
            })
            .ToList();
}
