using Blazor.Shared.SystemInformation.Services;
using Microsoft.Extensions.DependencyInjection;
using ViciOne.SystemMonitoring;

namespace Blazor.Shared.SystemInformation.Extensions;

public static class IServiceCollectionExtensions
{
    /// <summary>
    /// Adds system information services.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddSystemInformation(this IServiceCollection services)
    {
        services.AddSingleton<HardwareInfoService>();
        services.AddSingleton<MonitoringService>();
        services.AddSingleton<LogCenterService>();
        services.AddSingleton<IOutput, HardwareInfoService>(s => s.GetRequiredService<HardwareInfoService>());
        services.AddSingleton<IOutput, MonitoringService>(s => s.GetRequiredService<MonitoringService>());
        services.AddSingleton<IOutput, LogCenterService>(s => s.GetRequiredService<LogCenterService>());

        return services;
    }
}
