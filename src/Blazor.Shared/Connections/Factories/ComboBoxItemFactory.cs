using Sdk.Connections.Contracts;
using ViciOne.Ui.Blazor.Components.ComboBox;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Connections.Factories;

internal static class ComboBoxItemFactory
{
    public static List<ComboBoxItem<MqttConnectionType, string>> GetMqttConnectionTypes()
    {
        return Enum.GetValues<MqttConnectionType>()
            .Select(enumValue =>
            {
                var displayText = enumValue switch
                {
                    MqttConnectionType.TCP => TechnicalAcronyms.Tcp,
                    MqttConnectionType.TCPWithTLS => TechnicalTerms.TcpWithTls,
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
}
