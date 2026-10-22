using System.Globalization;
using Blazor.Shared.Localization;
using Sdk.Client.Connections;
using Sdk.Connections.Contracts;
using ViciOne.Ui.Localization.Resources;
using MqttLocalization = Blazor.Shared.Connections.Components.Localization.MqttSettings;

namespace Blazor.Shared.Connections.Validators;

public class MqttItemValidator : ItemValidatorBase<MqttConnection>
{
    /// <summary>
    /// MQTT 3.1.1 only guarantees client ids of up to 23 characters; brokers may reject longer ones.
    /// </summary>
    private const int MaximumV311ClientIdLength = 23;

    private static readonly string[] WebSocketSchemes = ["ws", "wss", "http", "https"];

    protected override void ValidateInternal(MqttConnection item)
    {
        ValidateAddress(item);
        ValidateRange(nameof(MqttConnection.Port), TechnicalTerms.Port, item.Port, 1, 65535);
        ValidateRange(nameof(MqttConnection.KeepAliveSeconds), MqttLocalization.KeepAlive, item.KeepAliveSeconds, 0, 65535);
        ValidateRange(nameof(MqttConnection.ConnectTimeoutSeconds), MqttLocalization.ConnectionTimeout, item.ConnectTimeoutSeconds, 1, 1000);
        ValidateCredentials(item);
        ValidateClientId(item);
        ValidateWill(item);

        if (item.SslProtocol is not null)
            ValidateClientCertificate(item);
    }

    private void ValidateAddress(MqttConnection item)
    {
        if (string.IsNullOrWhiteSpace(item.Address))
        {
            AddError(nameof(MqttConnection.Address), ValidationTerms.ProvideMqttBrokerAddress);
            return;
        }

        if (item.Protocol == MqttConnectionType.WebSocket)
        {
            if (TryGetWebSocketUri(item.Address, out var uri))
                ValidateWebSocketPort(item, uri);
            else if (!IsWebSocketHost(item.Address))
                AddError(nameof(MqttConnection.Address), ValidationTerms.ProvideMqttWebSocketAddress);

            return;
        }

        if (Uri.CheckHostName(item.Address) == UriHostNameType.Unknown)
            AddError(nameof(MqttConnection.Address), ValidationTerms.ProvideMqttBrokerHostName);

        // The client builds the WebSocket URI with UriBuilder, which throws for a bare IPv6 address such as ::1.
        static bool IsWebSocketHost(string address)
        {
            try
            {
                return Uri.CheckHostName(address) != UriHostNameType.Unknown && new UriBuilder(address).Host.Length > 0;
            }
            catch (UriFormatException)
            {
                return false;
            }
        }

        static bool TryGetWebSocketUri(string address, out Uri uri)
            => Uri.TryCreate(address, UriKind.Absolute, out uri!) && WebSocketSchemes.Contains(uri.Scheme);
    }

    /// <summary>
    /// The client replaces the address's port with <see cref="MqttConnection.Port"/>, so a differing one would be ignored silently.
    /// </summary>
    private void ValidateWebSocketPort(MqttConnection item, Uri uri)
    {
        if (!uri.IsDefaultPort && uri.Port != item.Port)
            AddError(nameof(MqttConnection.Port), string.Format(CultureInfo.CurrentCulture, ValidationTerms.MqttAddressPortMustMatchPort, uri.Port, item.Port));
    }

    private void ValidateRange(string key, string label, int value, int minimum, int maximum)
    {
        if (value < minimum || value > maximum)
            AddError(key, string.Format(CultureInfo.CurrentCulture, ValidationTerms.MqttValueMustBeBetween, label, minimum, maximum));
    }

    private void ValidateCredentials(MqttConnection item)
    {
        // MQTT 3.1.1 forbids a password without a user name, and the client drops it silently.
        if (!string.IsNullOrEmpty(item.Password) && string.IsNullOrWhiteSpace(item.Username))
            AddError(nameof(MqttConnection.Username), FieldIsRequired(TechnicalTerms.Username));
    }

    private void ValidateClientId(MqttConnection item)
    {
        if (item.ProtocolVersion == MqttProtocolVersion.V311 && item.ClientId?.Length > MaximumV311ClientIdLength)
        {
            AddError(nameof(MqttConnection.ClientId), string.Format(ValidationMessages.Culture,
                ValidationMessages.FieldMustNotHaveMoreThanXCharacters, MqttLocalization.ClientId, MaximumV311ClientIdLength));
        }
    }

    private void ValidateWill(MqttConnection item)
    {
        if (string.IsNullOrEmpty(item.WillTopic) && string.IsNullOrEmpty(item.WillMessage))
            return;

        if (string.IsNullOrWhiteSpace(item.WillTopic))
            AddError(nameof(MqttConnection.WillTopic), FieldIsRequired(MqttLocalization.WillTopic));
        else if (item.WillTopic.AsSpan().IndexOfAny('+', '#') >= 0)
            AddError(nameof(MqttConnection.WillTopic), ValidationTerms.MqttWillTopicMustNotContainWildcards);
    }

    private void ValidateClientCertificate(MqttConnection item)
    {
        var hasCertificate = !string.IsNullOrWhiteSpace(item.ClientCertificate);
        var hasKey = !string.IsNullOrWhiteSpace(item.ClientCertificateKey);

        if (hasKey && !hasCertificate)
            AddError(nameof(MqttConnection.ClientCertificate), FieldIsRequired(MqttLocalization.ClientCertificate));

        // Same rule as the client options builder, which loads both from files or both from PEM content.
        if (hasCertificate && hasKey && Path.IsPathRooted(item.ClientCertificate) != Path.IsPathRooted(item.ClientCertificateKey))
            AddError(nameof(MqttConnection.ClientCertificateKey), ValidationTerms.MqttCertificateAndKeyMustHaveTheSameForm);
    }

    private static string FieldIsRequired(string label)
        => string.Format(ValidationMessages.Culture, ValidationMessages.FieldIsRequired, label);
}
