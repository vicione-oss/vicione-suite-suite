using System.IO.Abstractions;
using System.Reflection;
using Blazor.DevAssets;
using Blazor.Server.Backend.Extensions;
using Blazor.Server.Backend.Localization;
using Blazor.Server.Backend.Middleware;
using Blazor.Server.Backend.Services;
using Blazor.Server.Backend.UserManagement;
using Blazor.Shared.Authorization;
using Blazor.Shared.Components;
using Blazor.Shared.Services;
using Blazor.Shared.UserManagement.Services;
using Core.Shared;
using Core.Shared.Messaging;
using Core.Shared.UserManagement.Contracts;
using Core.UiHosting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdk.Backend.Extensions;
using Sdk.Backend.Modules;
using Sdk.Client.Contracts;
using Sdk.Client.Extensions;
using Sdk.Client.Modules;
using Sdk.Client.Services;
using Sdk.Extensions;
using Sdk.Modules;
using ViciOne.Ui.MonochromeIcons.Assets.Extensions;

namespace Blazor.Server.Backend;

public sealed class BlazorServerBackendModule : BackendModule, IUiHostModule
{
    private readonly UiModuleManager _moduleHost = new();
    private readonly Lock _lock = new();
    private string? _defaultRequestCulture;


    /// <inheritdoc/>
    public void LoadUiDependencies(IServiceCollection services, IUiHostEnvironment uiEnvironment)
    {
        var moduleBundles = uiEnvironment.LoadModuleBundles(CreateBundle);
        _moduleHost.AddModuleBundles(moduleBundles);

        services.AddSingleton(_moduleHost);
        services.AddSingleton<IUiModuleManager>(s => s.GetRequiredService<UiModuleManager>());
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services, IConfiguration config, IMvcBuilder builder)
    {
        builder.Services.AddScoped<CircuitHandler, CultureCircuitHandler>();
        var options = config.BindSection<UiHostCircuitOptions>(ModuleId);
        services.AddLocalization();
        services.AddRazorPages();

        services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<SuiteUser>>();
        services.AddRazorComponents(opt =>
            {
                opt.DetailedErrors = options.EnableDetailedErrors;
            })
            .AddInteractiveServerComponents()
            .AddHubOptions(opt => opt.MaximumReceiveMessageSize = options.MaximumReceiveMessageSize);

        services.AddCascadingAuthenticationState();

        services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationHandler, ModuleAccessLevelHandler>();

        services.AddTransient<ExternalLoginService>();

        // ITicketStore lives in Core.OS because it needs the db context and other core parts.
        services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>>(
            sp => new CookieAuthConfigurator(sp.GetRequiredService<ITicketStore>()));

        services.ConfigureOptions<ExternalLoginFailureRedirect>();

        services.AddSingleton<IAppRenderingProvider, ServerRenderingProvider>();
        services.AddTransient(typeof(IUiEventPublisher<>), typeof(UiEventPublisher<>));
        services.AddTransient(typeof(IUiEventSubscriptionHolder<>), typeof(UiEventPublisher<>));
        services.AddSingleton(typeof(IUiEventSubscriptionRegistry<>), typeof(UiEventSubscriptionRegistry<>));
        services.AddLocalHttpClient();
        services.AddScoped<ILanguageCookieReader, LanguageCookieReader>();
        services.AddScoped<IUploadTicketFactory, UploadTicketFactory>();
        services.AddScoped<IStreamUploadHandlerFactory, StreamUploadHandlerFactory>();
        services.AddStreamUploadHandler<ImageUpload, BlazorServerBackendModule>((options) => options.FilenameTransform = (filename) => Core.Shared.Constants.DeviceImageFileName);
        services.AddStreamUploadHandler<BackupUpload, BlazorServerBackendModule>((options) => options.FilenameTransform = (filename) => Core.Shared.Constants.BackupFileName);
    }

    /// <inheritdoc/>
    public void ConfigureUiServices(IServiceCollection services, IUiHostEnvironment uiEnvironment, Action<string, Exception>? errorOccurred = null)
    {
        var failedModuleIds = new List<string>();

        AdjustFileSystemBasedMonochromeIconSvgMarkupProvider(services, uiEnvironment);

        services.AddUiHostClientServices();

        foreach (var module in _moduleHost.UiModules)
        {
            try
            {
                var moduleServices = new ServiceCollection();

                module.Configure?.Invoke(moduleServices);

                foreach (var service in moduleServices)
                    services.Add(service);
            }
            catch (Exception e)
            {
                errorOccurred?.Invoke(module.ModuleId, e);
                failedModuleIds.Add(module.ModuleId);
            }
        }

        foreach (var moduleId in failedModuleIds)
            _moduleHost.RemoveUiModuleBundle(moduleId);
    }

    /// <inheritdoc/>
    public void UseUiHost(IApplicationBuilder app, IWebHostEnvironment env, IUiHostEnvironment uiEnvironment)
    {
        var logger = GetLogger(app.ApplicationServices);
        if (uiEnvironment.IsDevelopment)
        {
            env.UseClientAssetsDevelopment(uiEnvironment, app.ApplicationServices.GetRequiredService<IFileSystem>(), logger);
        }
        else
        {
            // Works for the published client, e.g. path\to\publish\Client\wwwroot.
            env.UseClientAssetsProduction(uiEnvironment, logger);
        }

        app.UseDefaultFiles();

        app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = RevalidateStaticFile });
    }

    /// <summary>
    /// Makes the browser check a cached static file for a change before using it: a stylesheet kept from before an update
    /// would import scoped CSS bundles by a fingerprint that no longer exists.
    /// </summary>
    internal static void RevalidateStaticFile(StaticFileResponseContext context)
        => context.Context.Response.Headers.CacheControl = "no-cache";

    private IUiModuleBundle CreateBundle(string assemblyPath, Assembly assembly)
    {
        var modules = assembly.GetInstances<ClientModule>().ToList();
        if (modules.Count > 1)
            throw new InvalidOperationException("Multiple modules defined within assembly. Only one is allowed!");

        return new UiModuleBundle(modules.First(), assemblyPath, assembly);
    }

    /// <inheritdoc/>
    public void ConfigureIdentity(IdentityBuilder builder) { }

    /// <inheritdoc/>
    public void UseSecurity(IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();

        app.UseRequestLocalization(new RequestLocalizationOptions
        {
            DefaultRequestCulture = new(Shared.Constants.SupportedCultures.First()),
            SupportedCultures = Shared.Constants.SupportedCultures,
            SupportedUICultures = Shared.Constants.SupportedCultures,
            RequestCultureProviders = [new CookieRequestCultureProvider(), new DefaultRequestCultureProvider(this)]
        });

        app.UseMiddleware<OnboardingMiddleware>();
    }

    /// <inheritdoc/>
    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var logger = GetLogger(endpoints.ServiceProvider);

        // Covers controllers such as DebugController.
        endpoints.MapControllers();

        endpoints.MapExternalIdentityEndpoints();
        endpoints.MapAdditionalIdentityEndpoints();
        endpoints.MapPasskeyEndpoints();

        // Adds the authentication pages under /Account/*.
        endpoints.MapRazorPages();

        logger.LogInformation("Mapping endpoints for {Modules}", string.Join(", ", _moduleHost.UiModules.Select(k => k.ModuleId)));

        // Maps the blazor endpoints: blazor.web.js, / and the rest.
        endpoints.MapRazorComponents<App>()
            // The blazor.backend and module routes are added so the router resolves a deep link.
            .AddAdditionalAssemblies(_moduleHost.GetAdditionalAssemblies())
            // Merging the hosts would also allow the wasm render mode here.
            .AddInteractiveServerRenderMode();
    }

    private static ILogger GetLogger(IServiceProvider provider)
        => provider.GetRequiredService<ILogger<BlazorServerBackendModule>>();

    /// <inheritdoc/>
    public void SetDefaultRequestCulture(string? cultureName)
    {
        lock (_lock)
        {
            _defaultRequestCulture = cultureName;
        }
    }

    /// <inheritdoc/>
    public string? GetDefaultRequestCulture() => _defaultRequestCulture;

    private class UiModuleBundle(ClientModule module, string assemblyLocation, Assembly? assembly) : IUiModuleBundle
    {
        public IModule Module { get; } = module;
        public string AssemblyLocation { get; } = assemblyLocation;
        public Assembly? Assembly { get; } = assembly;
    }

    /// <summary>
    /// Falls back to the default request culture from <see cref="IUiHostModule"/> when
    /// <see cref="CookieRequestCultureProvider"/> supplies none.
    /// </summary>
    private class DefaultRequestCultureProvider(IUiHostModule uiHost) : IRequestCultureProvider
    {
        public async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
        {
            var defaultCulture = uiHost.GetDefaultRequestCulture();
            if (string.IsNullOrEmpty(defaultCulture))
                return null;

            return new ProviderCultureResult(defaultCulture);
        }
    }

    private static void AdjustFileSystemBasedMonochromeIconSvgMarkupProvider(
        IServiceCollection services,
        IUiHostEnvironment uiEnvironment)
    {
        if (uiEnvironment.IsDevelopment)
        {
            services.AddStaticWebAssetsRuntimeJsonBaseFilenameProvider<UiHostStaticWebAssetsRuntimeJsonBaseFilenameProvider>();
            services.AddStaticWebAssetsRuntimeJsonPathProvider<UiHostStaticWebAssetsRuntimeJsonPathProvider>();
        }
        else
        {
            services.AddFileSystemBasedContentRootProvider<UiHostFileSystemBasedContentRootProvider>();
        }
    }
}
