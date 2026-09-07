using System.Diagnostics.CodeAnalysis;
using Core.OS.Connections.Mqtt;
using Core.OS.Extensions;
using Core.OS.Instance;
using Core.Shared.Monitoring;
using Microsoft.Extensions.Options;
using Sdk.Journal;
using ViciOne.Journal;
using ViciOne.SystemMonitoring;
using ViciOne.SystemMonitoring.Configuration;

namespace Core.OS.Monitoring.Extensions;

internal static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        internal IServiceCollection AddJournalService()
        {
            if (OperatingSystem.IsLinux())
            {
                services.AddOptions<JournalOptions>();
                services.AddSingleton<IJournalService, JournalService>();
            }
            else
            {
                services.AddSingleton<IJournalService, NoOpJournalService>();
            }
            return services;
        }

        [SuppressMessage("CodeQuality", "IDE0079:Unnötige Unterdrückung entfernen")]
        [SuppressMessage("Interoperability", "CA1416:Plattformkompatibilität überprüfen")]
        internal IServiceCollection AddSystemMonitoring(IConfiguration configuration)
        {
            var options = configuration.GetSection(SystemMonitoringOptions.ConfigSection).Get<SystemMonitoringOptions>();

            services.AddSingleton<IJournalMonitoring, JournalMonitoring>(_ =>
            {
                var journalMonitoring = new JournalMonitoring();
                if (OperatingSystem.IsLinux() && options is not null && options.Enabled)
                    journalMonitoring.AddFilterEntry(JournalFieldsMetric.Suite, ViciOne.Ui.Localization.Resources.TechnicalTerms.Suite, options.SuiteJournalFilter, 0);
                return journalMonitoring;
            });

            services.AddUnvalidatedSuiteOptions<SystemMonitoringOptions>(SystemMonitoringOptions.ConfigSection,
                "A toggle, an interface name and journal filter strings - nothing with a range the suite can state.");

            if (OperatingSystem.IsLinux() && (options?.Enabled ?? true))
            {
                services.AddOptions<MonitoringConfig>()
                    .Configure<ILocalInstanceInformationProvider, IOptions<MqttClientOptions>, ILogger<MonitoringConfig>, IOptions<SystemMonitoringOptions>>(CreateConfiguration);
                services.AddSystemMonitoring(static b => b
                    .UseMqttOutput()
                    .UseDefaultCollectors()
                    .AddCollector<JournalFieldsCollector>());

                if (options?.Collectors.JournalFields ?? false)
                {
                    services.Configure<JournalFieldsCollectorOptions>(o =>
                    {
                        o.StringComparer = StringComparer.OrdinalIgnoreCase;
                        o.Fields = [JournalPriorityConstants.FieldName];
                    });
                }
            }
            return services;
        }
    }

    private static void CreateConfiguration(MonitoringConfig config, ILocalInstanceInformationProvider localInstanceInformationProvider, IOptions<MqttClientOptions> mqttClientOptions, ILogger<MonitoringConfig> logger, IOptions<SystemMonitoringOptions> options)
    {
        config.ClusterNodeId = localInstanceInformationProvider.ReadLocalInstanceId().ToString();
        config.NodeType = NodeType.DefaultDebian;

        var mqttOptions = mqttClientOptions.Value.ServiceClient;

        if (mqttOptions is null)
        {
            logger.LogError("Failed to configure SystemMonitoring, no MQTT options found.");
            return;
        }
        if (string.IsNullOrEmpty(mqttOptions.Endpoint))
        {
            logger.LogError("Failed to configure SystemMonitoring, no endpoint found.");
            return;
        }

        config.Mqtt.Host = mqttOptions.Endpoint;
        config.Mqtt.Port = ((ushort?)mqttOptions.Port) ?? 1883;
        config.Mqtt.Username = mqttOptions.UserName;
        config.Mqtt.Password = mqttOptions.Password;

        var collectors = options.Value.Collectors;

        config.EnabledMetrics[Collectors.Cpu] = collectors.Cpu;
        config.EnabledMetrics[Collectors.Ram] = collectors.Ram;
        config.EnabledMetrics[Collectors.Disk] = collectors.Disk;
        config.EnabledMetrics[Collectors.Network] = collectors.Network;
        config.EnabledMetrics[Collectors.Process] = collectors.Process;
        config.EnabledMetrics[Collectors.Processor] = collectors.Processor;
        config.EnabledMetrics[Collectors.ThermalSensors] = collectors.ThermalSensors;
        config.EnabledMetrics[Collectors.Uptime] = collectors.Uptime;
        config.EnabledMetrics[Collectors.JournalFields] = collectors.JournalFields;
        config.EnabledMetrics[Collectors.LoadAverage] = collectors.LoadAvg;
    }
}
