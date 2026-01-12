using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Core.OS.Tests;

public sealed class EmptyTestStartup
{
    /// <summary>
    /// setup the basic requirements for our webapp
    /// </summary>
    /// <param name="services"></param>
#pragma warning disable IDE0060 // Nicht verwendete Parameter entfernen
    public static void ConfigureServices(IServiceCollection services)
    {
        // config is done in app factory
    }

    /// <summary>
    /// not used in fixture ?!
    /// </summary>
    /// <param name="app"></param>
    public static void Configure(IApplicationBuilder app)
    {
        // configure is done in app factory or test
    }
#pragma warning restore IDE0060 // Nicht verwendete Parameter entfernen
}
