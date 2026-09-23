using System.ComponentModel;
using System.IO.Abstractions;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using MQTTnet;
using Sdk.Connections.Contracts;
using Sdk.Connections.Extensions;

namespace Core.Shared.Extensions;

public static class MqttClientOptionsBuilderExtensions
{
    /// <summary>
    /// For testing MQTT connections and the mqtt-viewer development tool. Accepts any certificate,
    /// including self-signed, without validating the chain.
    /// </summary>
    public static MqttClientOptionsBuilder WithSuiteConnection(this MqttClientOptionsBuilder builder,
        MqttConnection connection, IFileSystem fileSystem)
    {
        if (!string.IsNullOrWhiteSpace(connection.Username))
        {
            if (string.IsNullOrEmpty(connection.Password))
                builder.WithCredentials(connection.Username);
            else
                builder.WithCredentials(connection.Username, connection.Password);
        }

        if (!string.IsNullOrWhiteSpace(connection.ClientId))
            builder.WithClientId(connection.ClientId);

        builder
            .WithCleanSession(connection.CleanSession)
            .WithProtocolAndVersion(connection)
            .WithSslProtocol(connection, fileSystem)
            .WithWillOptions(connection);

        return builder;
    }

    private static MqttClientOptionsBuilder WithProtocolAndVersion(this MqttClientOptionsBuilder builder, MqttConnection connection)
    {
        switch (connection.Protocol)
        {
            case MqttConnectionType.TCP:
                builder.WithTcpServer(connection.Address, connection.Port);
                break;
            case MqttConnectionType.WebSocket:
                builder.WithWebSocketServer(b => b.WithUri(connection.GetWebsocketUri().ToString()));
                break;
            default:
                throw new InvalidEnumArgumentException(nameof(connection.Protocol),
                    (int)connection.Protocol,
                    typeof(MqttConnectionType));
        }

        builder.WithProtocolVersion(ToMqttProtocolVersion(connection.ProtocolVersion));

        return builder;

        static MQTTnet.Formatter.MqttProtocolVersion ToMqttProtocolVersion(MqttProtocolVersion proctocolVersion) => proctocolVersion switch
        {
            MqttProtocolVersion.V311 => MQTTnet.Formatter.MqttProtocolVersion.V311,
            MqttProtocolVersion.V500 => MQTTnet.Formatter.MqttProtocolVersion.V500,
            _ => throw new InvalidEnumArgumentException(nameof(proctocolVersion), (int)proctocolVersion, typeof(MqttProtocolVersion)),
        };
    }

    private static MqttClientOptionsBuilder WithSslProtocol(this MqttClientOptionsBuilder builder, MqttConnection connection, IFileSystem fileSystem)
    {
        if (string.IsNullOrWhiteSpace(connection.ClientCertificate) && connection.SslProtocol is null)
            return builder;

        var tlsOptions = new MqttClientTlsOptions
        {
            UseTls = true,
            AllowUntrustedCertificates = connection.AllowUntrustedCertificates,
        };

        // A client certificate takes precedence over plain TLS.
        if (connection.ClientCertificate is not null)
        {
            // Accepting all certificates is insecure but required for self-signed ones.
            if (tlsOptions.AllowUntrustedCertificates && !string.IsNullOrWhiteSpace(connection.ClientCertificate))
            {
                tlsOptions.CertificateValidationHandler = _ => true;
            }

            using var clientCert = CreateOrLoadClientCertificate(
                connection.ClientCertificate,
                connection.ClientCertificateKey,
                connection.ClientCertificateKeyPassword,
                fileSystem);

            if (clientCert is not null)
            {
                var pkcs12 = clientCert.Export(X509ContentType.Pkcs12);

#pragma warning disable SYSLIB0057 // Workaround for certificate not marked as exportable or persistent
                var fixedCert = new X509Certificate2(
                    pkcs12,
                    (string?)null,
                    X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet
                );
#pragma warning restore SYSLIB0057

                tlsOptions.ClientCertificatesProvider = new DefaultMqttCertificatesProvider(
                    new List<X509Certificate2> { fixedCert });
            }

            builder.WithTlsOptions(tlsOptions);

            return builder;
        }

        // No client certificate, but a TLS version was selected.
        if (connection.SslProtocol is MqttSslProtocol.Tls12 or MqttSslProtocol.Tls13)
        {
            builder.WithTlsOptions(tls =>
            {
                tls.UseTls();
                tls.WithSslProtocols(ToSslProtocols(connection.SslProtocol ?? throw new InvalidOperationException("SslProtocol error")));
            });
        }

        return builder;

        static SslProtocols ToSslProtocols(MqttSslProtocol proctocol) => proctocol switch
        {
            MqttSslProtocol.Tls12 => SslProtocols.Tls12,
            MqttSslProtocol.Tls13 => SslProtocols.Tls13,
            _ => throw new InvalidEnumArgumentException(nameof(proctocol), (int)proctocol, typeof(MqttSslProtocol)),
        };
    }

    private static MqttClientOptionsBuilder WithWillOptions(this MqttClientOptionsBuilder builder, MqttConnection connection)
    {
        if (string.IsNullOrWhiteSpace(connection.WillMessage) || string.IsNullOrWhiteSpace(connection.WillTopic))
            return builder;

        builder.WithWillPayload(connection.WillMessage);
        builder.WithWillTopic(connection.WillTopic);
        builder.WithWillRetain(connection.WillRetain);
        builder.WithWillQualityOfServiceLevel(ToMqttQualityOfServiceLevel(connection.QualityOfService));

        return builder;

        static MQTTnet.Protocol.MqttQualityOfServiceLevel ToMqttQualityOfServiceLevel(MqttQualityOfServiceLevel serviceLevel) => serviceLevel switch
        {
            MqttQualityOfServiceLevel.AtMostOnce => MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce,
            MqttQualityOfServiceLevel.AtLeastOnce => MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce,
            MqttQualityOfServiceLevel.ExactlyOnce => MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce,
            _ => throw new InvalidEnumArgumentException(nameof(serviceLevel), (int)serviceLevel, typeof(MqttQualityOfServiceLevel)),
        };
    }

    private static X509Certificate2? CreateOrLoadClientCertificate(string clientCertificate, string? clientCertificateKey, string? clientCertificateKeyPassword, IFileSystem fileSystem)
    {
        if (fileSystem.Path.IsPathRooted(clientCertificate)
            && (string.IsNullOrEmpty(clientCertificateKey) || fileSystem.Path.IsPathRooted(clientCertificateKey)))
        {
            if (!fileSystem.File.Exists(clientCertificate))
                throw new FileNotFoundException($"Could not find certificate file in {clientCertificate}");

            if (!string.IsNullOrEmpty(clientCertificateKey) && !fileSystem.File.Exists(clientCertificateKey))
                throw new FileNotFoundException($"Could not find private key file in {clientCertificateKey}");

            return fileSystem.CreateFromPemFile(clientCertificate, clientCertificateKey, clientCertificateKeyPassword);
        }

        if (fileSystem.Path.IsPathRooted(clientCertificate) || fileSystem.Path.IsPathRooted(clientCertificateKey))
            throw new InvalidOperationException("The certificates and the certificate key must either both be a file path, or none");

        if (!string.IsNullOrWhiteSpace(clientCertificateKeyPassword))
            return X509Certificate2.CreateFromEncryptedPemFile(clientCertificate, clientCertificateKeyPassword, clientCertificateKey);

        return X509Certificate2.CreateFromPem(clientCertificate, clientCertificateKey);
    }

    private static X509Certificate2 CreateFromPemFile(this IFileSystem fileSystem, string certPemFilePath, string? keyPemFilePath = null, string? certificateKeyPassword = null)
    {
        var certContents = fileSystem.File.ReadAllText(certPemFilePath);
        var keyContents = keyPemFilePath is null ? certContents : fileSystem.File.ReadAllText(keyPemFilePath);

        if (!string.IsNullOrWhiteSpace(certificateKeyPassword))
            return X509Certificate2.CreateFromEncryptedPemFile(certContents, certificateKeyPassword, keyContents);

        return X509Certificate2.CreateFromPem(certContents, keyContents);
    }
}
