using System.ComponentModel;
using System.IO.Abstractions;
using System.Security.Cryptography.X509Certificates;
using MQTTnet;
using MQTTnet.Formatter;
using Sdk.Connections.Contracts;
using Sdk.Connections.Extensions;

namespace Core.Shared.Extensions;

public static class MqttClientOptionsBuilderExtensions
{
    /// <summary>
    ///     Used for testing Mqtt-connections or connecting the mqtt-viewer (development-) tool.
    ///     Will accept any certificate including self-signed without validating the certificate chain
    /// </summary>
    public static MqttClientOptionsBuilder WithSuiteConnection(this MqttClientOptionsBuilder builder,
        MqttConnection connection, IFileSystem fileSystem)
    {
        builder.WithProtocolVersion(MqttProtocolVersion.V500);

        if (!string.IsNullOrWhiteSpace(connection.Username))
        {
            if (string.IsNullOrEmpty(connection.Password))
                builder.WithCredentials(connection.Username);
            else
                builder.WithCredentials(connection.Username, connection.Password);
        }

        if (connection.ClientCertificate is not null)
        {
            var tlsOptions = new MqttClientTlsOptions
            {
                UseTls = true,
                AllowUntrustedCertificates = true,
                CertificateValidationHandler = _ => true
                // Accept all certs. This is not very secure, but necessary for self-signed certs
            };
            using var clientCert = CreateOrLoadClientCertificate(connection.ClientCertificate,
                connection.ClientCertificateKey, fileSystem);
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
        }

        if (!string.IsNullOrWhiteSpace(connection.ClientId))
            builder.WithClientId(connection.ClientId);

        builder.WithCleanSession(connection.CleanSession);

        if (!string.IsNullOrWhiteSpace(connection.WillMessage) && !string.IsNullOrWhiteSpace(connection.WillTopic))
        {
            builder.WithWillPayload(connection.WillMessage);
            builder.WithWillTopic(connection.WillTopic);
            builder.WithWillRetain(connection.WillRetain);
        }

        switch (connection.Protocol)
        {
            case MqttConnectionType.TCP:
                builder.WithTcpServer(connection.Address, connection.Port);
                break;
            case MqttConnectionType.TCPWithTLS:
                builder.WithTcpServer(connection.Address, connection.Port)
                    .WithTlsOptions(tls =>
                    {
                        tls.UseTls();
                    });
                break;
            case MqttConnectionType.WebSocket:
                builder.WithWebSocketServer(b => b.WithUri(connection.GetWebsocketUri().ToString()));
                break;
            default:
                throw new InvalidEnumArgumentException(nameof(connection.Protocol),
                    (int)connection.Protocol,
                    typeof(MqttConnectionType));
        }

        return builder;
    }

    private static X509Certificate2? CreateOrLoadClientCertificate(string clientCertificate, string? clientCertificateKey, IFileSystem fileSystem)
    {
        if (fileSystem.Path.IsPathRooted(clientCertificate)
            && (string.IsNullOrEmpty(clientCertificateKey) || fileSystem.Path.IsPathRooted(clientCertificateKey)))
        {
            if (!fileSystem.File.Exists(clientCertificate))
                throw new FileNotFoundException($"Could not find certificate file in {clientCertificate}");
            if (!string.IsNullOrEmpty(clientCertificateKey) && !fileSystem.File.Exists(clientCertificateKey))
                throw new FileNotFoundException($"Could not find private key file in {clientCertificateKey}");
            return fileSystem.CreateFromPemFile(clientCertificate, clientCertificateKey);
        }

        if (fileSystem.Path.IsPathRooted(clientCertificate) || fileSystem.Path.IsPathRooted(clientCertificateKey))
            throw new InvalidOperationException("The certificates and the certificate key must either both be a file path, or none");

        return X509Certificate2.CreateFromPem(clientCertificate, clientCertificateKey);
    }

    private static X509Certificate2 CreateFromPemFile(this IFileSystem fileSystem, string certPemFilePath, string? keyPemFilePath = null)
    {
        var certContents = fileSystem.File.ReadAllText(certPemFilePath);
        var keyContents = keyPemFilePath is null ? certContents : fileSystem.File.ReadAllText(keyPemFilePath);
        return X509Certificate2.CreateFromPem(certContents, keyContents);
    }
}
