using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Core.OS.Tests;

public sealed class EmptyTestStartup
{
    /// <summary>
    /// setup the basic requirements for our webapp
    /// </summary>
#pragma warning disable IDE0060 // Nicht verwendete Parameter entfernen
    public static void ConfigureServices(IServiceCollection services)
    {
        // Configuration happens in the app factory.
    }

    /// <summary>
    /// not used in fixture ?!
    /// </summary>
    public static void Configure(IApplicationBuilder app)
    {
        // Configuration happens in the app factory or the test.
    }
#pragma warning restore IDE0060 // Nicht verwendete Parameter entfernen
}
