using System.Reflection;
using Core.OS.HostManagement.Handlers;
using Core.Shared.HostManagement;
using HostManagement.Shared.Communication.NamedPipe.Client;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Sdk.SystemConfiguration;

namespace Core.OS.HostManagement.Extensions;

internal static partial class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public void AddHostManagement(IConfiguration config)
        {
            var options = config.GetHostManagementOptions();

            services.AddSingleton<SystemConfigurationCache>();
            services.AddTransient<IControlServiceManagement, ControlServiceManagement>();

            services.AddPipeClient(options);

            AddCallbackHandlers(services);
            return;

            static void AddCallbackHandlers(IServiceCollection services)
            {
                var handlerDescriptors = Assembly.GetExecutingAssembly()
                    .DefinedTypes
                    .Where(t => t.ImplementedInterfaces.Contains(typeof(IPipeEventSubscriber)))
                    .Select(t => new ServiceDescriptor(typeof(IPipeEventSubscriber), t, ServiceLifetime.Singleton));
                services.TryAddEnumerable(handlerDescriptors);
            }
        }

        public IServiceCollection AddPipeClient(HostManagementOptions options)
        {
            services.TryAddSingleton(CreateCallbackHandlerRegistry);

            if (options.MockClient is not null && options.MockClient.Enabled)
            {
                services.AddSingleton(_ => Options.Create(options.MockClient));
                services.AddSingleton<IPipeClient, MockPipeClient>();
            }
            else
            {
                services.AddSingleton<IPipeClient, PipeClient>();
            }

            return services;
        }
    }

    private static CallbackHandlerRegistry CreateCallbackHandlerRegistry(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<CallbackHandlerRegistry>>();
        var registry = new CallbackHandlerRegistry();

        foreach (var handler in serviceProvider.GetServices<IPipeEventSubscriber>())
            handler.RegisterWith(registry);

        registry.OnMissingHandler += message => LogNoHandlerRegistered(logger, message.Topic);

        return registry;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No handler registered for topic {MessageTopic}")]
    private static partial void LogNoHandlerRegistered(ILogger<CallbackHandlerRegistry> logger, string messageTopic);
}
