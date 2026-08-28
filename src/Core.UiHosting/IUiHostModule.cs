using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Core.UiHosting;

/// <summary>
/// Lifecycle interface for a dynamically loaded module that acts as a UI host.
/// Implementations are responsible for bootstrapping the complete UI pipeline —
/// from dependency registration through middleware configuration to request culture management.
/// </summary>
public interface IUiHostModule
{
    /// <summary>
    /// Unique identifier for this module.
    /// </summary>
    string ModuleId { get; }

    /// <summary>
    /// Loads and registers the module bundles contributed by dynamically discovered UI assemblies.
    /// Called early in the service registration lifecycle before <see cref="ConfigureUiServices"/>.
    /// </summary>
    void LoadUiDependencies(IServiceCollection services, IUiHostEnvironment uiEnvironment);

    /// <summary>
    /// Allows the module to participate in ASP.NET Core Identity configuration,
    /// e.g. adding custom token providers or user validators.
    /// </summary>
    void ConfigureIdentity(IdentityBuilder builder);

    /// <summary>
    /// Registers the services contributed by each loaded client UI module into the application's
    /// service collection. Modules that fail during configuration are removed from the active module set.
    /// </summary>
    void ConfigureUiServices(IServiceCollection services, IUiHostEnvironment uiEnvironment, Action<string, Exception>? errorOccurred = null);

    /// <summary>
    /// Configures the security middleware pipeline including authentication,
    /// authorization, antiforgery, and request localization.
    /// </summary>
    void UseSecurity(IApplicationBuilder app);

    /// <summary>
    /// Configures the middleware pipeline for serving static web assets.
    /// In development, assets are resolved from the local build output;
    /// in production, assets are served from the published client output.
    /// </summary>
    void UseUiHost(IApplicationBuilder app, IWebHostEnvironment env, IUiHostEnvironment uiEnvironment);

    /// <summary>
    /// Sets the fallback culture used when no request culture provider (e.g. cookie) supplies a value.
    /// </summary>
    void SetDefaultRequestCulture(string? cultureName);

    /// <summary>
    /// Returns the currently configured fallback culture name, or <see langword="null"/> if none has been set.
    /// </summary>
    string? GetDefaultRequestCulture();
}
