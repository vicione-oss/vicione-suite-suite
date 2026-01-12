using Blazor.Shared.Localization;
using Sdk.Client.Connections;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Connections.Validators;

public class MqttItemValidator : ItemValidatorBase<MqttConnection>
{
    protected override void ValidateInternal(MqttConnection item)
    {
        if (string.IsNullOrWhiteSpace(item.Address))
        {
            AddError(nameof(MqttConnection.Address), ValidationTerms.ProvideMqttBrokerAddress);
        }
    }
}
