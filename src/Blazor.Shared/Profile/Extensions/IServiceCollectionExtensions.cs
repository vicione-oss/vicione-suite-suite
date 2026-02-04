using Blazor.Shared.Profile.Services.TicketCenter;
using Microsoft.Extensions.DependencyInjection;

namespace Blazor.Shared.Profile.Extensions;

public static class IServiceCollectionExtensions
{
    /// <summary>
    /// Adds user profile services.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddProfile(this IServiceCollection services)
    {
        services.AddScoped<IUrgentProvider, DummyUrgentProvider>();
        services.AddScoped<ITodayProvider, DummyTodayProvider>();
        services.AddScoped<IThisWeekProvider, DummyThisWeekProvider>();
        services.AddScoped<ISoonProvider, DummySoonProvider>();

        return services;
    }
}
