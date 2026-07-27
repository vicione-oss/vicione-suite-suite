using System.IO.Abstractions;
using System.Reflection;
using Core.Module.Options;
using Core.OS.Connections.Extensions;
using Core.OS.DataProtection.Extensions;
using Core.OS.DbContext;
using Core.OS.Diagnostics;
using Core.OS.HostManagement.Extensions;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.OS.Logging;
using Core.OS.Mail.Extensions;
using Core.OS.MessageBus.Extensions;
using Core.OS.MessageBus.MassTransit.Configuration;
using Core.OS.Modules;
using Core.OS.Modules.Services;
using Core.OS.Monitoring.Extensions;
using Core.OS.Persistence;
using Core.OS.UserManagement.Configuration;
using Core.OS.UserManagement.Extensions;
using Core.Shared.HostManagement;
using Core.Shared.Logging;
using Core.Shared.UserManagement.Configuration;
using MassTransit.Logging;
using Sdk.Backend.Persistence;
using MassTransit.Monitoring;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Core.Artifacts;
using Microsoft.FeatureManagement;
using Constants = Core.Shared.Constants;
using Sdk.Backend.IO;
using Core.OS.Modules.Extensions;
using Core.OS.Hosting;

namespace Core.OS.Extensions;

internal static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection ConfigureAndValidateOptions(InstanceOptions instanceOptions)
        {
            services.AddSuiteOptions<InstanceOptions>(InstanceOptions.ConfigSection);
            services.AddSuiteOptions<UserManagementOptions>(UserManagementOptions.ConfigSection);
            services.AddSuiteOptions<ModuleLoaderOptions>(ModuleLoaderOptions.ConfigSection);
            services.AddSuiteOptions<MessageBusOptions>(MessageBusOptions.ConfigSection);
            services.AddSuiteOptions<LoggingOptions>(LoggingOptions.ConfigSection);
            services.AddSuiteOptions<HostManagementOptions>(HostManagementOptions.ConfigSection);
            services.AddSuiteOptions<ArtifactRepositoryOptions>(ArtifactRepositoryOptions.ConfigSection);
            services.AddSuiteOptions<ExternalIdProviderOptions>(ExternalIdProviderOptions.ConfigSection);

            services.AddTransient<ILogOptions>(s => s.GetRequiredService<IOptions<LoggingOptions>>().Value)
                .Configure<HealthCheckPublisherOptions>(options =>
                {
                    options.Delay = TimeSpan.FromSeconds(instanceOptions.HealthChecks?.PublishDelayInSeconds ?? 30);
                    options.Timeout = options.Period
                        = TimeSpan.FromSeconds(instanceOptions.HealthChecks?.PublishIntervalInSeconds ?? 60);
                    //Timeout is the same as the period
                });

            return services;
        }

        public void AddSuiteOptions<T>(string sectionName) where T : class
            => services.AddOptions<T>()
                .BindConfiguration(sectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

        public IServiceCollection AddSuiteServices(ConfigurationManager config, SuitePreparationContext context)
        {
            if (context.ModuleContext is null)
                throw new InvalidOperationException("ModulePreparationContext is not set in SuitePreparationContext.");

            if (context.ModuleContext.ModuleHost is null)
                throw new InvalidOperationException("ModuleHost is not set in ModulePreparationContext.");

            if (context.ModuleContext.ModuleOptionsStore is null)
                throw new InvalidOperationException("ModuleOptionsStore is not set in ModulePreparationContext.");

            if (context.ModuleContext.RepositoryOptionsCache is null)
                throw new InvalidOperationException("RepositoryOptionsCache is not set in ModulePreparationContext.");

            services.AddModuleArtifactQueryApi(context.ModuleContext.RepositoryOptionsCache);
            services.AddModuleServices(context.ModuleContext.ModuleHost, context.ModuleContext.ModuleOptionsStore);
            services.AddSingleton(context.FileSystem);

            services.AddCoreServices(config, context.ModuleContext.ModuleHost);
            services.AddMailing();
            services.AddSystemMonitoring(config);
            services.AddJournalService();
            services.ConfigureDataProtection();

            return services;
        }

        internal IServiceCollection AddCoreServices(
            ConfigurationManager config,
            IModuleHost moduleHost)
        {
            var instanceOptions = config.GetInstanceOptions();
            var messageBusOptions = config.GetMessageBusOptions();

            services
                .AddTransient<IAtomicFileWriter, AtomicFileWriter>()
                .AddSingleton<IModuleDbContextRegistrar>(new ModuleDbContextRegistrar())
                .AddCoreDbContexts()
                .AddInstanceServices(instanceOptions, messageBusOptions.UseInMemoryBus)
                .AddConnectionServices();

            services.AddIdentityAndExternalAuth(config, moduleHost);

            // here we should have a valid configuration and loaded assemblies
            moduleHost.AddModuleServices(services);

#if DEBUG
            services.AddHostedService<ApplicationPartsLogger>();
#endif

            // MessageBus
            services.AddMassTransitMessageBus(config,
                moduleHost.ConfigureBusRegistrationConfigurator,
                [.. moduleHost.GetModuleAssemblies()
                    .Union(
                    [
                        Assembly.GetExecutingAssembly()
                    ])]);

            // The following must come after the MessageBus is ready
            services.AddHostedService<ApplicationWorker>();

            services.AddHealthChecks()
                .AddInstanceHealthChecks(instanceOptions);

            services.AddHostManagement(config);
            services.AddUserManagement();

            services.AddMemoryCache();

            services.AddFeatureManagement();

            return services;
        }

        private IServiceCollection AddCoreDbContexts()
        {
            services.RegisterModuleDbContext<IApplicationDbContext, ApplicationDbContextSqlite, ApplicationDbContextPostgres>(
                Constants.SystemModuleId,
                typeof(SystemBackendModule),
                ApplicationDbContext.DbSchemaName,
                enableSynchronization: true);

            services.RegisterModuleDbContext<IUserDbContext, UserDbContextSqlite, UserDbContextPostgres>(
                Constants.SystemModuleId,
                typeof(SystemBackendModule),
                UserDbContext.DbSchemaName,
                enableSynchronization: true);

            return services;
        }

        internal IServiceCollection AddSuiteOpenTelemetry(IConfiguration configuration,
            InstanceOptions instanceOptions,
            IFileSystem fileSystem)
        {
            const string OtelEndpoint = "OTEL_EXPORTER_OTLP_ENDPOINT";
            const string OtelServiceName = "OTEL_SERVICE_NAME";
            const string OtelAdditionalMeters = "OTEL_ADDITIONAL_METERS";
            const string OtelAdditionalSources = "OTEL_ADDITIONAL_SOURCES";

            if (IsDisabled())
                return services;

            services
                .AddOpenTelemetry()
                .ConfigureResource(b =>
                {
                    b.AddService(
                            configuration.GetValue<string?>(OtelServiceName) ?? CoreActivitySource.SourceName,
                            serviceNamespace: "vicione",
                            serviceVersion: Assembly.GetExecutingAssembly()
                                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                ?.InformationalVersion,
                            serviceInstanceId: fileSystem.File.ReadAllText(fileSystem.GetLocalInstanceIdFilePath(instanceOptions)).Trim(),
                            autoGenerateServiceInstanceId: false)
                        .AddAttributes(
                        [
                            new("process.pid", Environment.ProcessId),
                            new("service.instance.type", instanceOptions.Type.ToString())
                        ]);
                })
                .WithTracing(b => ConfigureTracing(b, CoreActivitySource.SourceName, configuration))
                .WithMetrics(b => ConfigureMetrics(b, configuration))
                .UseOtlpExporter();

            return services;

            bool IsDisabled() => string.IsNullOrEmpty(configuration.GetValue<string?>(OtelEndpoint));

            static void ConfigureTracing(TracerProviderBuilder builder, string serviceName, IConfiguration configuration)
            {
                builder
                    .AddAspNetCoreInstrumentation()
                    .AddSource(serviceName)
                    .AddSource(DiagnosticHeaders.DefaultListenerName);

                var additionalSources = configuration.GetSection(OtelAdditionalSources).Get<string[]>() ?? [];
                builder.AddSource(additionalSources);
            }

            static void ConfigureMetrics(MeterProviderBuilder builder, IConfiguration configuration)
            {
                builder
                    .AddRuntimeInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddMeter(InstrumentationOptions.MeterName);

                var additionalMeters = configuration.GetSection(OtelAdditionalMeters).Get<string[]>() ?? [];
                foreach (var meter in additionalMeters)
                    builder.AddMeter(meter);
            }
        }
    }
}
