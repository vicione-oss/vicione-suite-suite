using System.Globalization;
using Core.OS.Logging;
using Microsoft.Extensions.Logging;
using Sdk.Instance;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.Extensions;

public static class TestConfigExtensions
{
    extension(TestConfig config)
    {
        public TestConfig ConfigureLogging(Action<LoggingOptions>? configureSettings = null)
        {
            var settings = new LoggingOptions
            {
                LogLevel = new LoggingLogLevelOptions
                {
                    Default = LogLevel.Information,
                    Microsoft = LogLevel.Information
                },
                Resources = new LoggingResourceOptions
                {
                    Memory = new LoggingMemoryOptions()
                    {
                        LimitInMb = 200,
                        LimitInPercent = 70
                    }
                },
                LogTargets = []
            };
            configureSettings?.Invoke(settings);

            if (settings.LogTargets is not null)
            {
                var idx = 0;
                foreach (var logTarget in settings.LogTargets)
                    config.SetSetting($"{TestConfigKeys.Logging.LogTargets}:{idx++}", logTarget);
            }

            config.SetSetting(TestConfigKeys.Logging.LogPath, settings.LogPath);
            config.SetSetting(TestConfigKeys.Logging.LogLevelDefault, settings.LogLevel.Microsoft.ToString());
            config.SetSetting(TestConfigKeys.Logging.MemoryLimitInMb, (settings.Resources?.Memory?.LimitInMb ?? 200).ToString(CultureInfo.InvariantCulture));
            config.SetSetting(TestConfigKeys.Logging.MemoryLimitInPercent,
                (settings.Resources?.Memory?.LimitInPercent ?? 70).ToString(CultureInfo.InvariantCulture));
            return config;
        }

        public TestConfig UseInMemoryBus(bool enable = true)
            => config.SetSetting(TestConfigKeys.MessageBus.UseInMemoryBus, enable.ToString());

        public TestConfig UseInstanceType(InstanceType instance)
            => config.SetSetting(TestConfigKeys.Instance.Type, Enum.GetName(instance));

        public TestConfig AddMqttWebsocketClientOptions(string endpoint = "wss://localhost/mqtt",
            int port = 5001,
            string topicFilter = "topic-wsc",
            string user = "user-wsc",
            string pwd = "pwd-wsc")
        {
            var settings = new Dictionary<string, string?>
            {
                { TestConfigKeys.MqttWebSocketClient.Endpoint, endpoint },
                { TestConfigKeys.MqttWebSocketClient.Port, port.ToString(CultureInfo.InvariantCulture) },
                { TestConfigKeys.MqttWebSocketClient.UserName, user },
                { TestConfigKeys.MqttWebSocketClient.Password, pwd },
                { TestConfigKeys.MqttWebSocketClient.TopicFilter, topicFilter },
            };

            return config.AddCustomSettings(settings);
        }

        public TestConfig AddMqttServiceClientOptions(string endpoint = "localhost",
            int port = 1883,
            string topicFilter = "topic-sc",
            string user = "user-sc",
            string pwd = "pwd-sc")
        {
            var settings = new Dictionary<string, string?>
            {
                { TestConfigKeys.MqttServiceClient.Endpoint, endpoint },
                { TestConfigKeys.MqttServiceClient.Port, port.ToString(CultureInfo.InvariantCulture) },
                { TestConfigKeys.MqttServiceClient.UserName, user },
                { TestConfigKeys.MqttServiceClient.Password, pwd },
                { TestConfigKeys.MqttServiceClient.TopicFilter, topicFilter },
            };

            return config.AddCustomSettings(settings);
        }

        public TestConfig AddInstanceOptions(string homeDirectory = "AppData",
            string cacheDirectory = "Cache",
            InstanceType type = InstanceType.Standalone,
            Guid? idPreload = null,
            string? namePreload = null,
            string? descriptionPreload = null)
        {
            var settings = new Dictionary<string, string?>
            {
                { TestConfigKeys.Instance.IdPreload, idPreload?.ToString() },
                { TestConfigKeys.Instance.HomeDirectory, homeDirectory },
                { TestConfigKeys.Instance.CacheDirectory, cacheDirectory },
                { TestConfigKeys.Instance.Type, type.ToString() },
                { TestConfigKeys.Instance.NamePreload, namePreload },
                { TestConfigKeys.Instance.DescriptionPreload, descriptionPreload },
            };

            return config.AddCustomSettings(settings);
        }

        public TestConfig AddModuleLoaderOptions(string? manifestSeedPath = null,
            string modulesPath = "Modules",
            string? uiHost = Module.Constants.BlazorServerModuleId,
            string? uiHostsPath = "UiHosts",
            int packageCacheLifetime = 300000,
            bool enableModuleInstallation = true)
        {
            var settings = new Dictionary<string, string?>
            {
                { TestConfigKeys.ModuleLoader.AllowInstallation, enableModuleInstallation.ToString() },
                { TestConfigKeys.ModuleLoader.ManifestSeedPath, manifestSeedPath },
                { TestConfigKeys.ModuleLoader.ModulesPath, modulesPath },
                { TestConfigKeys.ModuleLoader.ModuleDebugPaths, packageCacheLifetime.ToString(CultureInfo.InvariantCulture) },
                { TestConfigKeys.ModuleLoader.UiHost, uiHost },
                { TestConfigKeys.ModuleLoader.UiHostsPath, uiHostsPath },
            };

            return config.AddCustomSettings(settings);
        }
    }
}
