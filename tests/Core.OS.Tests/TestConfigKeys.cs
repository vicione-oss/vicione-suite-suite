using Core.Module.Options;
using Core.OS.Connections.Mqtt;
using Core.OS.Instance;
using Core.OS.Logging;
using Core.OS.MessageBus.MassTransit.Configuration;

namespace Core.OS.Tests;

internal static class TestConfigKeys
{
    public static class Instance
    {
        public const string HomeDirectory = $"{InstanceOptions.ConfigSection}:{nameof(InstanceOptions.HomeDirectory)}";
        public const string CacheDirectory = $"{InstanceOptions.ConfigSection}:{nameof(InstanceOptions.CacheDirectory)}";
        public const string Type = $"{InstanceOptions.ConfigSection}:{nameof(InstanceOptions.Type)}";
        public const string IdPreload = $"{InstanceOptions.ConfigSection}:{nameof(InstanceOptions.IdPreload)}";
        public const string NamePreload = $"{InstanceOptions.ConfigSection}:{nameof(InstanceOptions.NamePreload)}";
        public const string DescriptionPreload = $"{InstanceOptions.ConfigSection}:{nameof(InstanceOptions.DescriptionPreload)}";
        public const string UseExternalSecurity = $"{InstanceOptions.ConfigSection}:{nameof(InstanceOptions.UseExternalSecurity)}";
    }

    public static class Logging
    {
        public const string LogPath = $"{LoggingOptions.ConfigSection}:{nameof(LoggingOptions.LogPath)}";
        public const string LogTargets = $"{LoggingOptions.ConfigSection}:{nameof(LoggingOptions.LogTargets)}";

        public const string MemoryLimitInMb
            = $"{LoggingOptions.ConfigSection}:{LoggingResourceOptions.ConfigSection}:{LoggingMemoryOptions.ConfigSection}:{nameof(LoggingMemoryOptions.LimitInMb)}";

        public const string MemoryLimitInPercent
            = $"{LoggingOptions.ConfigSection}:{LoggingResourceOptions.ConfigSection}:{LoggingMemoryOptions.ConfigSection}:{nameof(LoggingMemoryOptions.LimitInPercent)}";

        public const string LogLevelDefault = $"{LoggingOptions.ConfigSection}:{LoggingLogLevelOptions.ConfigSection}:{nameof(LoggingLogLevelOptions.Default)}";
    }

    public static class MessageBus
    {
        public const string UseInMemoryBus = $"{MessageBusOptions.ConfigSection}:{nameof(MessageBusOptions.UseInMemoryBus)}";
    }

    public static class ModuleApi
    {
        public const string Endpoint = $"{Sdk.Constants.ModuleApiSection}:{nameof(ModuleApiOptions.Endpoint)}";
        public const string UserName = $"{Sdk.Constants.ModuleApiSection}:{nameof(ModuleApiOptions.UserName)}";
        public const string Password = $"{Sdk.Constants.ModuleApiSection}:{nameof(ModuleApiOptions.Password)}";
        public const string PackageCacheLifetimeMs = $"{Sdk.Constants.ModuleApiSection}:{nameof(ModuleApiOptions.PackageCacheLifetimeMs)}";
    }

    public static class ModuleLoader
    {
        public const string AllowInstallation = $"{Sdk.Constants.ModuleLoaderSection}:{nameof(ModuleLoaderOptions.AllowInstallation)}";
        public const string DumpMappingFilePath = $"{Sdk.Constants.ModuleLoaderSection}:{nameof(ModuleLoaderOptions.DumpMappingFilePath)}";
        public const string ManifestSeedPath = $"{Sdk.Constants.ModuleLoaderSection}:{nameof(ModuleLoaderOptions.ManifestSeedPath)}";
        public const string ModulesPath = $"{Sdk.Constants.ModuleLoaderSection}:{nameof(ModuleLoaderOptions.ModulesPath)}";
        public const string ModuleDebugPaths = $"{Sdk.Constants.ModuleLoaderSection}:{nameof(ModuleLoaderOptions.ModuleDebugPaths)}";
        public const string UiHost = $"{Sdk.Constants.ModuleLoaderSection}:{nameof(ModuleLoaderOptions.UiHost)}";
        public const string UiHostsPath = $"{Sdk.Constants.ModuleLoaderSection}:{nameof(ModuleLoaderOptions.UiHostsPath)}";
    }

    public static class MqttWebSocketClient
    {
        public static readonly string Endpoint = MqttClientConnection.Endpoint(nameof(MqttClientOptions.WebSocketClient));
        public static readonly string Port = MqttClientConnection.Port(nameof(MqttClientOptions.WebSocketClient));
        public static readonly string UserName = MqttClientConnection.UserName(nameof(MqttClientOptions.WebSocketClient));
        public static readonly string Password = MqttClientConnection.Password(nameof(MqttClientOptions.WebSocketClient));
        public static readonly string TopicFilter = MqttClientConnection.TopicFilter(nameof(MqttClientOptions.WebSocketClient));
    }

    public static class MqttServiceClient
    {
        public static readonly string Endpoint = MqttClientConnection.Endpoint(nameof(MqttClientOptions.ServiceClient));
        public static readonly string Port = MqttClientConnection.Port(nameof(MqttClientOptions.ServiceClient));
        public static readonly string UserName = MqttClientConnection.UserName(nameof(MqttClientOptions.ServiceClient));
        public static readonly string Password = MqttClientConnection.Password(nameof(MqttClientOptions.ServiceClient));
        public static readonly string TopicFilter = MqttClientConnection.TopicFilter(nameof(MqttClientOptions.ServiceClient));
    }

    private static class MqttClientConnection
    {
        private const string Section = "MqttClient";

        public static string Endpoint(string key)
            => $"{Section}:{key}:{nameof(MqttConnectionOptions.Endpoint)}";

        public static string Port(string key)
            => $"{Section}:{key}:{nameof(MqttConnectionOptions.Port)}";

        public static string UserName(string key)
            => $"{Section}:{key}:{nameof(MqttConnectionOptions.UserName)}";

        public static string Password(string key)
            => $"{Section}:{key}:{nameof(MqttConnectionOptions.Password)}";

        public static string TopicFilter(string key)
            => $"{Section}:{key}:{nameof(MqttConnectionOptions.TopicFilter)}";
    }
}
