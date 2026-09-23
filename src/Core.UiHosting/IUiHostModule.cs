using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Core.UiHosting;

/// <summary>
/// Lifecycle hooks for a dynamically loaded module that bootstraps the UI pipeline, from service
/// registration through middleware to request culture.
/// </summary>
public interface IUiHostModule
{
    string ModuleId { get; }

    /// <summary>
    /// Loads the bundles contributed by discovered UI assemblies. Runs before <see cref="ConfigureUiServices"/>.
    /// </summary>
    void LoadUiDependencies(IServiceCollection services, IUiHostEnvironment uiEnvironment);

    /// <summary>
    /// Hook for custom token providers, user validators and other ASP.NET Core Identity configuration.
    /// </summary>
    void ConfigureIdentity(IdentityBuilder builder);

    /// <summary>
    /// Registers each loaded client UI module's services. A module that throws is removed from the active set.
    /// </summary>
    void ConfigureUiServices(IServiceCollection services, IUiHostEnvironment uiEnvironment, Action<string, Exception>? errorOccurred = null);

    /// <summary>
    /// Adds authentication, authorization, antiforgery and request localization to the middleware pipeline.
    /// </summary>
    void UseSecurity(IApplicationBuilder app);

    /// <summary>
    /// Serves static web assets from the local build output in development, from the published client
    /// output otherwise.
    /// </summary>
    void UseUiHost(IApplicationBuilder app, IWebHostEnvironment env, IUiHostEnvironment uiEnvironment);

    /// <summary>
    /// Sets the fallback culture used when no request culture provider, such as the cookie, supplies one.
    /// </summary>
    void SetDefaultRequestCulture(string? cultureName);

    /// <summary>
    /// Returns the fallback culture name, or <see langword="null"/> if none is set.
    /// </summary>
    string? GetDefaultRequestCulture();
}
