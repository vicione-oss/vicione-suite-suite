using System.Reflection;
using Core.OS.Extensions;
using Core.Shared.HostManagement;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sdk.SystemConfiguration;

namespace Core.OS.HostManagement.Extensions;

internal static class IServiceCollectionExtensions
{
    public static void AddHostManagement(this IServiceCollection services, IConfiguration config)
    {
        var options = config.GetHostManagementOptions();

        services.AddSingleton<EventCallbackRegistry>();
        services.AddSingleton<SystemConfigurationCache>();
        services.AddTransient<IControlServiceManagement, ControlServiceManagement>();

        if (options.MockClient is not null && options.MockClient.Enabled)
        {
            services.Configure<MockPipeClientOptions>(opt =>
                config.Bind($"{HostManagementOptions.ConfigSection}:{nameof(HostManagementOptions.MockClient)}", opt));

            services.AddSingleton<IPipeClient, MockPipeClient>();
        }
        else
        {
            services.AddSingleton<IPipeClient, PipeClient>();
        }

        AddCallbackHandlers(services);
        return;

        static void AddCallbackHandlers(IServiceCollection services)
        {
            var handlerDescriptors = Assembly.GetExecutingAssembly()
                .DefinedTypes
                .Where(t => t.ImplementedInterfaces.Contains(typeof(ICallbackHandler)))
                .Select(t => new ServiceDescriptor(typeof(ICallbackHandler), t, ServiceLifetime.Transient));
            services.TryAddEnumerable(handlerDescriptors);
        }
    }
}
