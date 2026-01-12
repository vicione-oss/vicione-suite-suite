using System.IO.Abstractions;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.OS.MessageBus.MassTransit.Configuration;
using MassTransit;
using MassTransit.Serialization;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Core.OS.MessageBus.MassTransit;

internal static class RabbitMqCleaner
{
    public static async Task CleanVirtualHost(IServiceProvider provider, ILogger logger,
        CancellationToken cancellationToken)
    {
        var fileSystem = provider.GetRequiredService<IFileSystem>();
        var busOptions = provider.GetRequiredService<IOptions<MessageBusOptions>>();
        var transportOptions = provider.GetRequiredService<IOptions<RabbitMqTransportOptions>>();
        var instanceOptions = provider.GetRequiredService<IOptions<InstanceOptions>>();

        if (busOptions.Value is { UseInMemoryBus: false, CleanVirtualHost: true } && !CleanedThisVersion(fileSystem, instanceOptions.Value))
            await CleanVirtualHost(fileSystem, transportOptions.Value, instanceOptions.Value, logger, cancellationToken);
    }

    private static async Task CleanVirtualHost(IFileSystem fileSystem, RabbitMqTransportOptions transportOptions,
        InstanceOptions instanceOptions,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var virtualHost = transportOptions.VHost;

        var factory = new ConnectionFactory
        {
            HostName = transportOptions.Host,
            Port = transportOptions.Port,
            VirtualHost = virtualHost ?? "/",
            UserName = transportOptions.User,
            Password = transportOptions.Pass
        };

        var connection = await factory.CreateConnectionAsync(cancellationToken);
        try
        {

            await using var channel = await connection.CreateChannelAsync(options: null, cancellationToken);
            // RabbitMQ.Client >= 7.0.0 removed ConfirmSelect but added MaybeConfirmSelect within usage of CreateChannelAsync:
            // https://github.com/rabbitmq/rabbitmq-dotnet-client/blob/58ac94966d12ffbb88ccd03ea085b3516a544321/projects/RabbitMQ.Client/Impl/Channel.cs#L385
            // channel.ConfirmSelect();

            var exchangeCount = 0;
            var queueCount = 0;

            var exchanges = await GetVirtualHostEntities(transportOptions, "exchanges", cancellationToken);
            foreach (var exchange in exchanges)
            {
                await channel.ExchangeDeleteAsync(exchange, cancellationToken: cancellationToken);
                exchangeCount++;
            }

            var queues = await GetVirtualHostEntities(transportOptions, "queues", cancellationToken);
            foreach (var queue in queues)
            {
                await channel.QueuePurgeAsync(queue, cancellationToken);
                await channel.QueueDeleteAsync(queue, cancellationToken: cancellationToken);
                queueCount++;
            }

            await channel.CloseAsync(cancellationToken);

            if (exchangeCount > 0 || queueCount > 0)
                logger.LogInformation("Removed {QueueCount} queue(s), {ExchangeCount} exchange(s)",
                    queueCount,
                    exchangeCount);


            await connection.CloseAsync(200, "Completed (Ok)", cancellationToken);
            WriteCleanedVersionFile(fileSystem, instanceOptions);
        }
        catch (Exception ex)
        {
            if (connection.IsOpen)
                await connection.CloseAsync(500, $"Completed (not OK): {ex.Message}", cancellationToken);
        }
    }

    private static async Task<IList<string>> GetVirtualHostEntities(RabbitMqTransportOptions transportOptions, string element, CancellationToken cancellationToken)
    {
        using var client = GetHttpClient(transportOptions);

        var builder = GetUriBuilder(transportOptions, $"api/{element}/{transportOptions.VHost.Trim('/')}");

        var bytes = await client.GetByteArrayAsync(builder.Uri, cancellationToken);

        var rootElement = JsonSerializer.Deserialize<JsonElement>(bytes, SystemTextJsonMessageSerializer.Options);

        var entities = rootElement.EnumerateArray().Select(x => x.GetProperty("name").GetString()).ToArray();

        return entities.Where(x => !string.IsNullOrWhiteSpace(x) && !x.StartsWith("amq.", StringComparison.Ordinal)).Select(x => x!).ToList();
    }

    private static HttpClient GetHttpClient(RabbitMqTransportOptions transportOptions)
    {
        var client = new HttpClient();
        var byteArray = Encoding.ASCII.GetBytes($"{transportOptions.User}:{transportOptions.Pass}");
        client.DefaultRequestHeaders.Authorization
            = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        return client;
    }

    private static UriBuilder GetUriBuilder(RabbitMqTransportOptions transportOptions, string pathValue)
    {
        return new UriBuilder(transportOptions.UseSsl ? "https" : "http",
            transportOptions.Host,
            transportOptions.ManagementPort,
            pathValue);
    }


    private static bool CleanedThisVersion(IFileSystem fileSystem, InstanceOptions options)
        => Assembly.GetExecutingAssembly().GetName().Version <= ReadCleanedVersionFile(fileSystem, options);

    private static void WriteCleanedVersionFile(IFileSystem fileSystem, InstanceOptions options)
        => fileSystem.File.WriteAllText(GetCleanedNodesVersionFilePath(fileSystem, options), Assembly.GetExecutingAssembly().GetName().Version?.ToString());

    private static Version? ReadCleanedVersionFile(IFileSystem fileSystem, InstanceOptions options)
    {
        if (!fileSystem.File.Exists(GetCleanedNodesVersionFilePath(fileSystem, options)))
            return null;

        var version = File.ReadAllText(GetCleanedNodesVersionFilePath(fileSystem, options));
        return Version.Parse(version);
    }

    private static string GetCleanedNodesVersionFilePath(IFileSystem fileSystem, InstanceOptions options)
        => fileSystem.Path.Combine(fileSystem.GetRootedHomeDirectory(options), "CleanedBrokerVersion.info");
}
