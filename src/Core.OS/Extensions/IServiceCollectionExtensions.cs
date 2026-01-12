using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Reflection;
using Core.Module.Options;
using Core.OS.Connections.Extensions;
using Core.OS.Connections.Mqtt;
using Core.OS.DbContext;
using Core.OS.DbContext.Extensions;
using Core.OS.HostManagement.Extensions;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.OS.Logging;
using Core.OS.MessageBus.MassTransit.Configuration;
using Core.OS.Modules;
using Core.OS.Modules.Services;
using Core.OS.Monitoring;
using Core.OS.UserManagement.Configuration;
using Core.OS.UserManagement.Extensions;
using Core.Shared;
using Core.Shared.HostManagement;
using Core.Shared.Logging;
using Core.Shared.Monitoring;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Sdk.Journal;
using ViciOne.Journal;
using ViciOne.SystemMonitoring;
using ViciOne.SystemMonitoring.Configuration;

namespace Core.OS.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection ConfigureAndValidateOptions(this IServiceCollection services, InstanceOptions instanceOptions)
    {
        services.AddSuiteOptions<InstanceOptions>(InstanceOptions.ConfigSection);
        services.AddSuiteOptions<UserManagementOptions>(UserManagementOptions.ConfigSection);
        services.AddSuiteOptions<ModuleLoaderOptions>(Sdk.Constants.ModuleLoaderSection);
        services.AddSuiteOptions<MessageBusOptions>(MessageBusOptions.ConfigSection);
        services.AddSuiteOptions<LoggingOptions>(LoggingOptions.ConfigSection);
        services.AddSuiteOptions<HostManagementOptions>(HostManagementOptions.ConfigSection);
        services.AddSuiteOptions<ModuleApiOptions>(Sdk.Constants.ModuleApiSection);

        services.AddTransient<ILogOptions>(s => s.GetRequiredService<IOptions<LoggingOptions>>().Value)
            .Configure<HealthCheckPublisherOptions>(options =>
            {
                options.Delay = TimeSpan.FromSeconds(instanceOptions.HealthChecks?.PublishDelayInSeconds ?? 30);
                options.Timeout = options.Period = TimeSpan.FromSeconds(instanceOptions.HealthChecks?.PublishIntervalInSeconds ?? 60);
                //Timeout is the same as the period
            });

        return services;
    }

    private static void AddSuiteOptions<T>(this IServiceCollection services, string sectionName) where T : class
        => services.AddOptions<T>()
            .BindConfiguration(sectionName)
            .ValidateDataAnnotations();

    internal static IServiceCollection AddPlatformServices(this IServiceCollection services, IFileSystem fileSystem, ConfigurationManager config, IModuleHost moduleHost)
    {
        var instanceOptions = config.GetInstanceOptions();
        var messageBusOptions = config.GetMessageBusOptions();

        services
            .AddSingleton(fileSystem)
            .AddCoreDbContexts(fileSystem)
            .AddInstanceServices(instanceOptions, messageBusOptions.UseInMemoryBus)
            .AddConnectionServices();

        // ui host might not know identity or suite user
        moduleHost.AddUiHostServices(services, (svc) =>
        {
            return svc
                .AddIdentity<SuiteUser, IdentityRole>(options =>
                {
                    options.SignIn.RequireConfirmedAccount = true;
                    options.Password.RequiredLength = Constants.MinimumPasswordLength;
                })
                .AddEntityFrameworkStores<UserDbContext>();
        });

        // here we should have a valid configuration and loaded assemblies
        moduleHost.AddModuleServices(services);

#if DEBUG
        services.AddHostedService<ApplicationPartsLogger>();
#endif

        // MessageBus
        services.AddMassTransitMessageBus(config,
            moduleHost.ConfigureBusRegistrationConfigurator,
            moduleHost.GetModuleAssemblies().Union(
                [
                    Assembly.GetExecutingAssembly()
                ])
                .ToArray());

        // The following must come after the MessageBus is ready 
        services.AddHostedService<ApplicationWorker>();

        services.AddHealthChecks()
            .AddInstanceHealthChecks(instanceOptions);

        services.AddHostManagement(config);
        services.AddUserManagement();

        services.AddMemoryCache();

        return services;
    }

    private static IServiceCollection AddCoreDbContexts(this IServiceCollection services, IFileSystem fileSystem)
    {
        services.AddCoreDbContext<IApplicationDbContext, ApplicationDbContextSqlite, ApplicationDbContextPostgres>(ApplicationDbContext.DbSchemaName);

        services.AddCoreDbContext<IUserDbContext, UserDbContextSqlite, UserDbContextPostgres>(UserDbContext.DbSchemaName);

        return services;
    }

    [SuppressMessage("CodeQuality", "IDE0079:Unnötige Unterdrückung entfernen")]
    [SuppressMessage("Interoperability", "CA1416:Plattformkompatibilität überprüfen")]
    internal static IServiceCollection AddSystemMonitoring(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(SystemMonitoringOptions.ConfigSection).Get<SystemMonitoringOptions>();

        services.AddSingleton<IJournalMonitoring, JournalMonitoring>(_ =>
        {
            var journalMonitoring = new JournalMonitoring();
            if (OperatingSystem.IsLinux() && options is not null && options.Enabled)
                journalMonitoring.AddFilterEntry(JournalFieldsMetric.Suite, ViciOne.Ui.Localization.Resources.TechnicalTerms.Suite, options.SuiteJournalFilter, 0);
            return journalMonitoring;
        });

        services.AddOptions<SystemMonitoringOptions>()
            .BindConfiguration(SystemMonitoringOptions.ConfigSection)
            .ValidateDataAnnotations();

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
    }

    internal static IServiceCollection AddJournalService(this IServiceCollection services)
    {
        if (OperatingSystem.IsLinux())
        {
            services.AddOptions<JournalOptions>();
            services.AddSingleton<JournalService>();
        }
        return services;
    }
}
