using System.Reflection;
using Core.OS.Connections.Extensions;
using Core.OS.DataProtection.Extensions;
using Core.OS.DbContext.Extensions;
using Core.OS.Diagnostics.Extensions;
using Core.OS.EnvironmentOverrides.Extensions;
using Core.OS.HostManagement.Extensions;
using Core.OS.Hosting;
using Core.OS.Instance.Extensions;
using Core.OS.Logging.Extensions;
using Core.OS.Mail.Extensions;
using Core.OS.MessageBus.Extensions;
using Core.OS.MessageBus.MassTransit.Configuration;
using Core.OS.Modules;
using Core.OS.Modules.Extensions;
using Core.OS.Monitoring.Extensions;
using Core.OS.Persistence.Extensions;
using Core.OS.Security.Extensions;
using Core.OS.UserManagement.Extensions;
using Microsoft.FeatureManagement;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Core.OS.Configuration;
using Core.OS.Instance;
using Core.OS.UserManagement.Configuration;
using Core.Shared.UserManagement.Configuration;
using Core.Module.Options;
using Core.Shared.HostManagement;
using Core.Artifacts;

namespace Core.OS.Extensions;

internal static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// The single Core.OS composition entry point. Owns the ordering contract:
        /// diagnostics and options first, then the concern services.
        /// </summary>
        internal IServiceCollection AddCoreOs(ConfigurationManager config, SuitePreparationContext context)
        {
            if (context.ModuleContext is null)
                throw new InvalidOperationException("ModulePreparationContext is not set in SuitePreparationContext.");

            if (context.ModuleContext.ModuleHost is null)
                throw new InvalidOperationException("ModuleHost is not set in ModulePreparationContext.");

            if (context.ModuleContext.ModuleOptionsStore is null)
                throw new InvalidOperationException("ModuleOptionsStore is not set in ModulePreparationContext.");

            if (context.ModuleContext.RepositoryOptionsCache is null)
                throw new InvalidOperationException("RepositoryOptionsCache is not set in ModulePreparationContext.");

            services.AddCoreDiagnostics(config, context.InstanceOptions, context.FileSystem);

            services.AddModuleArtifactQueryApi(context.ModuleContext.RepositoryOptionsCache);
            services.AddModuleServices(context.ModuleContext.ModuleHost, context.ModuleContext.ModuleOptionsStore);
            services.AddSingleton(context.FileSystem);

            // bind and validate appsettings, env vars etc. before anything consumes them
            services.AddCoreOptions();
            services.AddCoreLogging();

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
                .AddPersistence()
                .AddEnvironmentOverrides()
                .AddCoreDbContexts()
                .AddInstanceServices(instanceOptions, messageBusOptions.UseInMemoryBus)
                .AddConnectionServices();

            services.AddTransportSecurity(instanceOptions);

            services.AddIdentityAndExternalAuth(config, moduleHost);

            // here we should have a valid configuration and loaded assemblies
            moduleHost.AddModuleServices(services);

            // MessageBus
            services.AddMassTransitMessageBus(config,
                moduleHost.ConfigureBusRegistrationConfigurator,
                [.. moduleHost.GetModuleAssemblies()
                    .Union(
                    [
                        Assembly.GetExecutingAssembly()
                    ])]);

            // The following must come after the MessageBus is ready
            services.AddModuleHosting();

            services.AddHealthChecks()
                .AddInstanceHealthChecks(instanceOptions);

            services.AddHostManagement(config);
            services.AddUserManagement();

            // Framework primitives shared by every concern, with no owning folder.
            services.AddMemoryCache();
            services.AddFeatureManagement();

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
        /// <see cref="Configuration"/>; unlike the <c>ValidateDataAnnotations()</c> this used to call, it
        /// recurses into the nested settings classes marked <see cref="ValidateObjectMembersAttribute"/> and so
        /// actually enforces their bounds. An options type with nothing to validate cannot supply a generated
        /// validator and goes through <see cref="AddUnvalidatedSuiteOptions{TOptions}"/> instead, which states why.
        /// </remarks>
        public void AddValidatedOptions<TOptions, TValidator>(string sectionName)
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
        /// <see cref="AddValidatedOptions{TOptions, TValidator}"/> is the better answer wherever a bound value has a
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

        /// <summary>
        /// Binds and validates the cross-cutting suite option sections in one place.
        /// Concern-local options (mail, monitoring, data protection, ...) are registered by
        /// their own concern; logging options are registered by <c>AddCoreLogging</c>.
        /// </summary>
        internal IServiceCollection AddCoreOptions()
        {
            services.AddValidatedOptions<InstanceOptions, InstanceOptionsValidator>(InstanceOptions.ConfigSection);
            services.AddValidatedOptions<UserManagementOptions, UserManagementOptionsValidator>(UserManagementOptions.ConfigSection);
            services.AddValidatedOptions<MessageBusOptions, MessageBusOptionsValidator>(MessageBusOptions.ConfigSection);
            services.AddValidatedOptions<ExternalIdProviderOptions, ExternalIdProviderOptionsValidator>(ExternalIdProviderOptions.ConfigSection);

            services.AddUnvalidatedSuiteOptions<ModuleLoaderOptions>(ModuleLoaderOptions.ConfigSection,
                "Paths and feature switches only - no bound value whose range the suite can state.");
            services.AddUnvalidatedSuiteOptions<HostManagementOptions>(HostManagementOptions.ConfigSection,
                "Pipe and service names plus a cache lifetime; the nested MockPipeClientOptions is a test seam.");
            services.AddUnvalidatedSuiteOptions<ArtifactRepositoryOptions>(ArtifactRepositoryOptions.ConfigSection,
                "Source endpoints are checked where they are used - JFrogArtifactRepository requires https - not at bind time.");

            return services;
        }
    }
}
