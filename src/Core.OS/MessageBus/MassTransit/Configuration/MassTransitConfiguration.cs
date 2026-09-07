using System.Collections.Frozen;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Serialization;
using Core.Module;
using Core.OS.DbContext;
using Core.OS.Diagnostics.MassTransit;
using Core.OS.Extensions;
using Core.OS.Instance;
using Core.OS.Instance.Consumers;
using Core.OS.Instance.Extensions;
using Core.OS.Instance.Initialization;
using Core.OS.Instance.Services;
using Core.OS.MessageBus.Extensions;
using Core.OS.Persistence.Consumers;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using MassTransit.Internals;
using MassTransit.Metadata;
using MassTransit.Middleware.Outbox;
using MassTransit.Util;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sdk.Backend.Messaging;
using Sdk.Backend.Persistence;
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
            // MassTransit.RabbitMqTransportOptions is a third-party type and carries no validation attributes, so it
            // cannot be given a source-generated validator and the suite will not annotate a type it does not own.
            // Exempted deliberately: a wrong host, port or credential surfaces as a broker connection failure at bus
            // start, which the transport already reports and retries - see ADR-004.
            services.AddUnvalidatedSuiteOptions<RabbitMqTransportOptions>(MessageBusOptions.TransportOptionsConfigSection,
                "Third-party type the suite must not annotate; a bad connection is reported by the transport at bus start.");

            services.AddSingleton<IEndpointNameFormatter, SuiteEndpointNameFormatter>();

            services.AddTransient<IRoutingSlipBuilder>(_ => new RoutingSlipBuilder(NewId.NextGuid()));
            services.AddTransient<IRoutingSlipBuilderFactory, RoutingSlipBuilderFactory>();

            var busSettings = config.GetMessageBusOptions();
            var useBusOutbox = instanceOptions.Type == InstanceType.Master && !busSettings.UseInMemoryBus;

            services.AddMassTransit(busConfig =>
            {
                // https://masstransit.io/documentation/configuration/usage-telemetry
                busConfig.DisableUsageTelemetry();

                registrationConfig.Invoke(busConfig);

                FrozenSet<string>? requestEndpoints = null;
                busConfig.AddConfigureEndpointsCallback((provider, queueName, configurator) =>
                {
                    var messageBusOptions = provider.GetRequiredService<IOptions<MessageBusOptions>>().Value;

                    // ADR-004 (D1): retry is configured per endpoint and for every transport, so a standalone edge
                    // device recovers the same way a clustered one does. Order is normative:
                    // retry -> message scope -> outbox -> consumer.
                    requestEndpoints ??= MessageRetryClassifier.FindRequestEndpoints(assembliesToScan,
                        provider.GetRequiredService<ILocalInstanceInformationProvider>().ReadLocalInstanceId());

                    var retryIntervals = MessageRetryClassifier.GetRetryIntervals(messageBusOptions,
                        MessageRetryClassifier.Classify(queueName, requestEndpoints));

                    // An explicitly empty ladder is the operator's "do not retry" (ADR-004 D2). Install no retry filter
                    // at all rather than a policy with zero intervals, so the endpoint is left exactly as it would be
                    // without the feature: one attempt, then the message faults.
                    if (retryIntervals.Length > 0)
                        configurator.UseMessageRetry(r =>
                        {
                            // ADR-004 (D2): deterministic failures are not worth a retry budget on a two core device.
                            r.Ignore<ArgumentException>();
                            r.Ignore<NotSupportedException>();
                            r.Intervals(retryIntervals);
                        });

                    // ADR-004 (D1): the message scope has to sit inside the retry filter, so every attempt resolves a
                    // fresh set of scoped dependencies. Configured on the bus it would wrap the endpoint filters
                    // instead, and all attempts of one message would share a single DI scope - retrying a consumer
                    // that failed inside SaveChangesAsync on the very same DbContext, change tracker still dirty,
                    // which is precisely the transient-database case the ladders exist for.
                    configurator.UseMessageScope(provider);

                    // ADR-004 (D1): the in memory outbox buffers everything a consumer publishes and discards it when
                    // that consumer throws. It has to sit inside the retry filter on every transport, so a retried
                    // attempt starts with an empty publish buffer instead of repeating the publishes of the attempt
                    // before it. Inside the message scope, so a scoped publish endpoint resolves to the buffered one.
                    configurator.UseInMemoryOutbox(provider);

                    if (configurator is not IRabbitMqReceiveEndpointConfigurator raq)
                        return;

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
                    if (string.Equals(queueName,
                            namingHelper.ExecuteActivity<SyncDataActivity, SyncDataArguments>(),
                            StringComparison.Ordinal)
                        || string.Equals(queueName,
                            namingHelper.Consumer<SyncRoutingSlipFaultedConsumer>(),
                            StringComparison.Ordinal))
                        return;
                    configurator.AddDependency(provider.GetRequiredService<SynchronizationState>());
                });

                busConfig.AddBusObserver<BusObserver>();

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
                    // Configure Bus Outbox on master to guarantee atomic publish (ADR-003 Gap 1)
                    if (useBusOutbox)
                    {
                        services.AddDbContext<OutboxDbContext>((sp, o) =>
                            o.UseNpgsql(sp.GetRequiredService<IMasterDbConnectionStringProvider>().ConnectionString));

                        busConfig.AddEntityFrameworkOutbox<OutboxDbContext>(o => o.UsePostgres());
                        busConfig.AddBusOutboxDelivery();
                    }

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

                        Configure(context, cfg, instanceOptions.Type);

                        // ADR-004 (D5): the lazily declared _error and _skipped queues inherit the input queue settings,
                        // which leaves them unbounded on a master. Bound them explicitly instead.
                        cfg.SendTopology.ConfigureErrorSettings = queue => FaultQueueTopology.Configure(queue, messageBusOptions.ErrorQueue);
                        cfg.SendTopology.ConfigureDeadLetterSettings = queue => FaultQueueTopology.Configure(queue, messageBusOptions.ErrorQueue);

                        if (messageBusOptions.PrefetchCount is not null)
                            cfg.PrefetchCount = messageBusOptions.PrefetchCount.Value;

                        cfg.ConfigureEndpoints(context);
                    });

                    busConfig.SetRabbitMqReplyToRequestClientFactory();
                }

                busConfig.AddUIForwardingConsumers();

                busConfig.AddInstanceDependentActions(instanceOptions, assembliesToScan);

                busConfig.AddRequestConsumers(assembliesToScan);
            });

            // Remove MassTransitHostedService to manually control bus start in our ApplicationWorker
            services.RemoveMassTransitHostedService();

            return services;
        }

        private void AddLocalBus(Assembly[] assembliesToScan)
            => services.AddMassTransit<ILocalBus>(busConfig =>
            {
                // Request consumers only: the local bus answers a slave's own requests without a round trip to the
                // master. ConsumesOnlyRequests rather than MessagingHelper.ConsumesRequest, which is vacuously true
                // for a message-less type and would also put every IConsumer<Fault<T>> on this bus - ADR-004 (D6)
                // registers fault consumers on master/standalone only.
                busConfig.AddConsumers(ConsumerTypeExtensions.ConsumesOnlyRequests, assembliesToScan);
                busConfig.UsingInMemory((context, cfg) =>
                {
                    cfg.UseMessageScope(context);
                    cfg.ConfigureEndpoints(context);
                });
            });

        private void AddRequestConsumers(Assembly[] assembliesToScan)
        {
            if (assembliesToScan.Length == 0)
                assembliesToScan = AppDomain.CurrentDomain.GetAssemblies();

            var types = AssemblyTypeCache.FindTypes(assembliesToScan, RegistrationMetadata.IsConsumerOrDefinition).GetAwaiter().GetResult();

            foreach (var consumerType in types.FindTypes(TypeClassification.Concrete | TypeClassification.Closed))
            {
                if (consumerType.BaseType is { IsGenericType: true } baseType
                    && (baseType.GetGenericTypeDefinition() == typeof(RequestConsumer<,>)
                        || baseType.GetGenericTypeDefinition() == typeof(InstanceDependentRequestConsumer<,>))
                    )
                {
                    services.AddScoped(baseType, consumerType);
                }
            }
        }
    }

    // These are the values UseBusOutbox() would have passed on from the Entity Framework outbox configurator.
    // OutboxDeliveryServiceOptions falls back to five seconds for both, so registering the delivery service
    // without them would shorten the delivery timing rather than leave it as it was.
    private static void MatchUseBusOutboxDeliveryTiming(OutboxDeliveryServiceOptions options)
    {
        options.QueryDelay = TimeSpan.FromSeconds(10);
        options.MessageDeliveryTimeout = TimeSpan.FromSeconds(10);
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

        // UseMessageScope is deliberately *not* configured here: a bus-level filter wraps the endpoint-level ones, so
        // it would sit outside the retry filter and every attempt of a message would share one DI scope. It is applied
        // per endpoint in AddConfigureEndpointsCallback instead. See ADR-004 (D1).
        cfg.UseExecuteActivityFilter(typeof(ExecuteActivityFilter<>), context);

        // ADR-004 (D4): stop a receive-endpoint while its dependencies are failing instead of draining the queue into
        // _error at full CPU. Installed as an observer, so it does not interfere with the retry/outbox ordering.
        var killSwitch = context.GetRequiredService<IOptions<MessageBusOptions>>().Value.KillSwitch;
        if (killSwitch.Enabled)
            cfg.UseKillSwitch(k => k
                .SetActivationThreshold(killSwitch.ActivationThreshold)
                .SetTripThreshold(killSwitch.TripThresholdPercent)
                .SetTrackingPeriod(TimeSpan.FromSeconds(killSwitch.TrackingPeriodInSeconds))
                .SetRestartTimeout(TimeSpan.FromSeconds(killSwitch.RestartTimeoutInSeconds)));

        if (instanceType == InstanceType.Standalone)
            cfg.ConcurrentMessageLimit = 1;
    }

    extension(IRegistrationConfigurator busConfig)
    {
        /// <summary>
        ///     Registers the delivery half of the Bus Outbox: the notification and the service that moves staged
        ///     messages to the transport. <c>UseBusOutbox()</c> would register the same two, but also replace the
        ///     scoped bus context with one that diverts every send and publish into the outbox change tracker
        ///     whenever the scope carries no <c>ConsumeContext</c>. Only replication stages into the outbox, and it
        ///     does so explicitly on the module's own transaction (see <c>BusOutboxReplicationPublisher</c>), so
        ///     everything else keeps talking to the transport directly.
        /// </summary>
        private void AddBusOutboxDelivery()
        {
            busConfig.AddSingleton<IBusOutboxNotification, BusOutboxNotification>();
            busConfig.AddHostedService<BusOutboxDeliveryService<OutboxDbContext>>();
            busConfig.AddOptions<OutboxDeliveryServiceOptions>().Configure(MatchUseBusOutboxDeliveryTiming);
        }

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
