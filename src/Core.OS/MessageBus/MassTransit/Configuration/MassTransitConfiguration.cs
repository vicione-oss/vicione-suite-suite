using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Serialization;
using Core.Module;
using Core.OS.Instance;
using Core.OS.Instance.Contracts;
using Core.OS.Instance.Extensions;
using Core.OS.Instance.Initialization;
using Core.OS.MessageBus.Extensions;
using Core.OS.Persistence.Consumers;
using MassTransit;
using MassTransit.Internals;
using MassTransit.Metadata;
using MassTransit.Util;
using Microsoft.Extensions.Options;
using Sdk.Backend.Messaging;
using Sdk.Instance;
using Sdk.Messaging;
using Sdk.Modules;

namespace Core.OS.MessageBus.MassTransit.Configuration;

#pragma warning disable CA1506
internal static class MassTransitConfiguration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddMassTransitMessageBus(IConfiguration config,
            Action<IBusRegistrationConfigurator> registrationConfig,
            Assembly[] assembliesToScan)
        {
            if (assembliesToScan.Length == 0)
                assembliesToScan = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(x => !x.GetName().Name?.StartsWith("MassTransit", StringComparison.OrdinalIgnoreCase) ?? true)
                    .ToArray();

            services.AddScoped<ISuiteMediator, BackEndMediator>();

            var instanceOptions = config.GetInstanceOptions();
            services.AddOptions<RabbitMqTransportOptions>().BindConfiguration(MessageBusOptions.TransportOptionsConfigSection);

            services.AddSingleton<IEndpointNameFormatter, SuiteEndpointNameFormatter>();

            services.AddTransient<IRoutingSlipBuilder>(_ => new RoutingSlipBuilder(NewId.NextGuid()));
            services.AddTransient<IRoutingSlipBuilderFactory, RoutingSlipBuilderFactory>();

            services.AddMassTransit(busConfig =>
            {
                // https://masstransit.io/documentation/configuration/usage-telemetry
                busConfig.DisableUsageTelemetry();

                registrationConfig.Invoke(busConfig);
                busConfig.AddConfigureEndpointsCallback((provider, queueName, configurator) =>
                {
                    if (configurator is not IRabbitMqReceiveEndpointConfigurator raq)
                        return;

                    var messageBusOptions = provider.GetRequiredService<IOptions<MessageBusOptions>>().Value;
                    if (instanceOptions.Type != InstanceType.Master)
                    {
                        raq.SetExchangeArgument("x-expires", (long)TimeSpan.FromDays(messageBusOptions.QueueLifetimeInDays).TotalMilliseconds);
                        raq.QueueExpiration = TimeSpan.FromDays(messageBusOptions.QueueLifetimeInDays);
                    }
                    raq.Durable = messageBusOptions.DurableQueues;

                    // Ensure InitialSync is finished before consuming other messages:
                    if (instanceOptions.Type != InstanceType.Slave)
                        return;
                    var namingHelper = provider.GetRequiredService<IEndpointNameFormatter>();
                    if (!string.Equals(queueName,
                            namingHelper.ExecuteActivity<SyncDataActivity, SyncDataArguments>(),
                            StringComparison.Ordinal))
                        configurator.AddDependency(provider.GetRequiredService<SynchronizationState>());
                });

                var busSettings = config.GetMessageBusOptions();
                if (busSettings.UseInMemoryBus)
                {
                    busConfig.UsingInMemory((context, cfg) =>
                    {
                        Configure(context, cfg, instanceOptions.Type);

                        cfg.ConfigureEndpoints(context);
                    });
                }
                else
                {
                    // ensure we can wait till bus got started
                    busConfig.AddOptions<MassTransitHostOptions>()
                        .Configure(options =>
                        {
                            // for multibus situation we can't wait because execution hangs in MT
                            // see ApplicationWorker comments in StartingAsync
                            options.WaitUntilStarted = instanceOptions.Type != InstanceType.Slave;
                        });

                    busConfig.UsingRabbitMq((context, cfg) =>
                    {
                        var messageBusOptions = context.GetRequiredService<IOptions<MessageBusOptions>>().Value;
                        cfg.UseMessageRetry(r => r.Intervals(messageBusOptions.RetryIntervals));

                        Configure(context, cfg, instanceOptions.Type);

                        // in memory outbox is only used with rabbit mq bus
                        cfg.UseInMemoryOutbox(context);

                        if (messageBusOptions.PrefetchCount is not null)
                            cfg.PrefetchCount = messageBusOptions.PrefetchCount.Value;

                        cfg.ConfigureEndpoints(context);
                    });

                    busConfig.SetRabbitMqReplyToRequestClientFactory();
                }

                busConfig.AddUIForwardingConsumers();

                busConfig.AddInstanceDependentActions(instanceOptions, assembliesToScan);
            });

            // Remove MassTransitHostedService to manually control bus start in our ApplicationWorker
            services.RemoveMassTransitHostedService();

            return services;
        }

        private void AddLocalBus(Assembly[] assembliesToScan)
            => services.AddMassTransit<ILocalBus>(busConfig =>
            {
                //Add all instance-independent endpoints
                busConfig.AddConsumers(MessagingHelper.ConsumesRequest, assembliesToScan);// Maybe only requests?
                busConfig.UsingInMemory((context, cfg) =>
                {
                    cfg.UseMessageScope(context);
                    cfg.ConfigureEndpoints(context);
                });
            });
    }

    private static void Configure<T>(IRegistrationContext context, T cfg, InstanceType instanceType)
        where T : IBusFactoryConfigurator
    {
        cfg.ConfigureJsonSerializerOptions(j =>
        {
            j.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            j.Converters.Add(DefaultJsonSerializerSettings.CreateDateTimeConverter());
            j.WriteIndented = false;
            return j;
        });

        cfg.UseMessageScope(context);
        cfg.UseExecuteActivityFilter(typeof(ExecuteActivityFilter<>), context);

        if (instanceType == InstanceType.Standalone)
            cfg.ConcurrentMessageLimit = 1;
    }

    extension(IRegistrationConfigurator busConfig)
    {
        private void AddUIForwardingConsumers()
        {
            var assemblies = AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(k => IsOfInterest(k.GetName().Name));

            var forwardedMessageTypes = AssemblyTypeCache.FindTypes(assemblies, t => t.HasAttribute<ForwardToUIAttribute>()).GetAwaiter().GetResult();
            foreach (var messageType in forwardedMessageTypes.FindTypes(TypeClassification.Concrete | TypeClassification.Interface))
            {
                var consumerType = typeof(EventForwardToUiConsumer<>).MakeGenericType(messageType);
                var consumerDefinitionType = typeof(EventForwardToUiConsumerDefinition<>).MakeGenericType(messageType);
                busConfig.AddConsumer(consumerType, consumerDefinitionType);
            }

            return;

            // filtering out the known assemblies (especially dx) reduces the scan time significantly
            static bool IsOfInterest(string? name)
            {
                if (string.IsNullOrEmpty(name))
                    return false;

                // these we can exclude safely
                if (name.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("System", StringComparison.OrdinalIgnoreCase))
                    return false;

                var sdkName = typeof(IModule).Assembly.GetName().Name;

                // while we keep our naming convention these are the only ones of interest
                if (name.EndsWith(Constants.ModuleSuffixPublic, StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(Constants.ModuleSuffixInternal, StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(Constants.ModuleSuffixBackend, StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("ViciOne.Suite.Core.", StringComparison.OrdinalIgnoreCase)
                    || (sdkName is not null && name.StartsWith(sdkName, StringComparison.OrdinalIgnoreCase)))
                    return true;

                return false;
            }
        }

        private void AddInstanceDependentActions(InstanceOptions instanceOptions, Assembly[] assembliesToScan)
        {
            switch (instanceOptions.Type)
            {
                case InstanceType.Slave:
                    //Add all instance-dependent endpoints
                    busConfig.AddConsumers(t => t.ConsumesInstanceDependentMessages() || t.HasAttribute<ReadOnlyConsumerAttribute>(),
                        assembliesToScan);
                    busConfig.AddExecuteActivity<SyncDataActivity, SyncDataArguments>();
                    busConfig.AddLocalBus(assembliesToScan);
                    break;
                case InstanceType.Master:
                    //Add all consumers and instance-independent endpoints
                    busConfig.AddConsumers(t => t != typeof(DbChangeSetConsumer), assembliesToScan);
                    busConfig.AddActivities(t => t != typeof(SyncDataActivity), assembliesToScan);
                    busConfig.AddSagaStateMachines(assembliesToScan);
                    break;
                case InstanceType.Standalone:
                    //Add all endpoints
                    busConfig.AddConsumers(t => t != typeof(DbChangeSetConsumer), assembliesToScan);
                    busConfig.AddActivities(t => t != typeof(SyncDataActivity), assembliesToScan);
                    busConfig.AddSagaStateMachines(assembliesToScan);
                    break;
                default:
                    throw new InvalidEnumArgumentException(nameof(instanceOptions.Type), (int)instanceOptions.Type, typeof(InstanceType));
            }
        }

        private void AddActivities(Func<Type, bool> filter, params Assembly[] assemblies)
        {
            if (assemblies.Length == 0)
                assemblies = AppDomain.CurrentDomain.GetAssemblies();

            var types = AssemblyTypeCache.FindTypes(assemblies, RegistrationMetadata.IsActivityOrDefinition).GetAwaiter().GetResult();

            busConfig.AddActivities(filter, types.FindTypes(TypeClassification.Concrete | TypeClassification.Closed).ToArray());
        }
    }
}
#pragma warning restore CA1506
