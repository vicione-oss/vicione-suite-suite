using System.IO.Abstractions;
using System.Reflection;
using Core.Artifacts;
using Core.Module.Options;
using Core.OS.Configuration;
using Core.OS.Connections.Extensions;
using Core.OS.DataProtection.Extensions;
using Core.OS.DbContext;
using Core.OS.Diagnostics;
using Core.OS.Diagnostics.Extensions;
using Core.OS.EnvironmentOverrides;
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
using MassTransit.Monitoring;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Sdk.Backend.IO;
using Core.OS.Modules.Extensions;
using Core.OS.Hosting;
using Sdk.Backend.Persistence;
using Constants = Core.Shared.Constants;

namespace Core.OS.Extensions;

internal static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection ConfigureAndValidateOptions(InstanceOptions instanceOptions)
        {
            services.AddSuiteOptions<InstanceOptions, InstanceOptionsValidator>(InstanceOptions.ConfigSection);
            services.AddSuiteOptions<UserManagementOptions, UserManagementOptionsValidator>(UserManagementOptions.ConfigSection);
            services.AddSuiteOptions<MessageBusOptions, MessageBusOptionsValidator>(MessageBusOptions.ConfigSection);
            services.AddSuiteOptions<LoggingOptions, LoggingOptionsValidator>(LoggingOptions.ConfigSection);
            services.AddSuiteOptions<ExternalIdProviderOptions, ExternalIdProviderOptionsValidator>(ExternalIdProviderOptions.ConfigSection);

            services.AddUnvalidatedSuiteOptions<ModuleLoaderOptions>(ModuleLoaderOptions.ConfigSection,
                "Paths and feature switches only - no bound value whose range the suite can state.");
            services.AddUnvalidatedSuiteOptions<HostManagementOptions>(HostManagementOptions.ConfigSection,
                "Pipe and service names plus a cache lifetime; the nested MockPipeClientOptions is a test seam.");
            services.AddUnvalidatedSuiteOptions<ArtifactRepositoryOptions>(ArtifactRepositoryOptions.ConfigSection,
                "Source endpoints are checked where they are used - JFrogArtifactRepository requires https - not at bind time.");

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

        /// <summary>
        /// Binds <typeparamref name="TOptions"/> to <paramref name="sectionName"/> and validates it with
        /// <typeparamref name="TValidator"/> on start, so a bad value fails the host instead of binding silently.
        /// </summary>
        /// <remarks>
        /// The validator is a required type argument rather than an optional call in a fluent chain, because a
        /// silently unvalidated options type is the failure mode this method exists to design out.
        /// <typeparamref name="TValidator"/> is a <c>[OptionsValidator]</c> partial class from
        /// <see cref="Core.OS.Configuration"/>; unlike the <c>ValidateDataAnnotations()</c> this used to call, it
        /// recurses into the nested settings classes marked <see cref="ValidateObjectMembersAttribute"/> and so
        /// actually enforces their bounds. An options type with nothing to validate cannot supply a generated
        /// validator and goes through <see cref="AddUnvalidatedSuiteOptions{TOptions}"/> instead, which states why.
        /// </remarks>
        public void AddSuiteOptions<TOptions, TValidator>(string sectionName)
            where TOptions : class
            where TValidator : class, IValidateOptions<TOptions>, new()
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<TOptions>, TValidator>());
            services.AddOptions<TOptions>()
                .BindConfiguration(sectionName)
                .ValidateOnStart();
        }

        /// <summary>
        /// Binds <typeparamref name="TOptions"/> to <paramref name="sectionName"/> without validating it, recording
        /// <paramref name="justification"/> as the reason it is exempt.
        /// </summary>
        /// <remarks>
        /// Only for types whose graph carries no validation attribute at all: the source generator emits no
        /// <c>Validate</c> method for those (it reports <c>SYSLIB1203</c>), so an <c>[OptionsValidator]</c> partial
        /// class would not compile. Annotating the type and using
        /// <see cref="AddSuiteOptions{TOptions, TValidator}"/> is the better answer wherever a bound value has a
        /// range the suite can state.
        /// </remarks>
        /// <param name="justification">Why this options type has nothing to validate.</param>
        public void AddUnvalidatedSuiteOptions<TOptions>(string sectionName, string justification)
            where TOptions : class
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<TOptions>>(
                new UnvalidatedSuiteOptions<TOptions>(justification)));
            services.AddOptions<TOptions>()
                .BindConfiguration(sectionName)
                .ValidateOnStart();
        }

        internal IServiceCollection AddTransportSecurity(InstanceOptions instanceOptions)
        {
            var safelist = TrustedProxySafelist.Parse(instanceOptions.TrustedProxies);

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                                           | ForwardedHeaders.XForwardedProto
                                           | ForwardedHeaders.XForwardedHost;

                // The default safelists trust loopback proxies only — exactly the packaged
                // nginx on an edge device. TrustedProxies extends them for proxies on other
                // hosts (see docs/oidc.md).
                foreach (var proxy in safelist.Proxies)
                    options.KnownProxies.Add(proxy);
                foreach (var network in safelist.Networks)
                    options.KnownIPNetworks.Add(network);

                // Empty safelists disable the source check entirely, so headers are trusted
                // from any sender. Kept only for the deprecated flag; TrustedProxies wins
                // when both are configured, as it is the stricter setting.
#pragma warning disable CS0618
                if (!instanceOptions.UseHeaderForwarding || safelist.Proxies.Count != 0 || safelist.Networks.Count != 0)
                    return;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });

            // Requests only ever reach the suite over TLS (nginx on devices, https endpoints
            // elsewhere), so the Secure attribute is enforced on all cookies globally.
            services.Configure<CookiePolicyOptions>(options => options.Secure = CookieSecurePolicy.Always);

            // Mirror the Strict-Transport-Security header the packaged nginx adds, so both
            // layers advertise the same policy.
            services.Configure<HstsOptions>(options =>
            {
                options.MaxAge = TimeSpan.FromDays(365);
                options.IncludeSubDomains = true;
                options.Preload = true;
            });

            return services;
        }

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
                .AddTransient<IEnvironmentOverridesRepository, EnvironmentOverridesRepository>()
                .AddSingleton<IModuleDbContextRegistrar>(new ModuleDbContextRegistrar())
                .AddCoreDbContexts()
                .AddInstanceServices(instanceOptions, messageBusOptions.UseInMemoryBus)
                .AddConnectionServices();

            services.AddTransportSecurity(instanceOptions);

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
            var exporterOptions = configuration.GetOtelExporterOptions();
            if (string.IsNullOrEmpty(exporterOptions.Endpoint))
                return services;

            services
                .AddOpenTelemetry()
                .ConfigureResource(b =>
                {
                    b.AddService(
                            SuiteOtelResource.GetServiceName(exporterOptions),
                            serviceNamespace: SuiteOtelResource.ServiceNamespace,
                            serviceVersion: SuiteOtelResource.GetServiceVersion(),
                            serviceInstanceId: fileSystem.ReadLocalInstanceId(instanceOptions),
                            autoGenerateServiceInstanceId: false)
                        .AddAttributes(
                        [
                            new("process.pid", Environment.ProcessId),
                            new("service.instance.type", instanceOptions.Type.ToString())
                        ]);
                })
                .WithTracing(b => ConfigureTracing(b, CoreActivitySource.SourceName, exporterOptions))
                .WithMetrics(b => ConfigureMetrics(b, exporterOptions))
                .UseOtlpExporter();

            return services;

            static void ConfigureTracing(TracerProviderBuilder builder, string serviceName, OtelExporterOptions exporterOptions)
            {
                builder
                    .AddAspNetCoreInstrumentation()
                    .AddSource(serviceName)
                    .AddSource(DiagnosticHeaders.DefaultListenerName);

                builder.AddSource(exporterOptions.AdditionalSources);
            }

            static void ConfigureMetrics(MeterProviderBuilder builder, OtelExporterOptions exporterOptions)
            {
                builder
                    .AddRuntimeInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddMeter(InstrumentationOptions.MeterName);

                foreach (var meter in exporterOptions.AdditionalMeters)
                    builder.AddMeter(meter);
            }
        }
    }
}
