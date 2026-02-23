using System.Reflection;
using Blazor.DevAssets;
using Blazor.Server.Backend.Extensions;
using Blazor.Server.Backend.Localization;
using Blazor.Server.Backend.Middleware;
using Blazor.Server.Backend.Services;
using Blazor.Shared.Authorization;
using Blazor.Shared.Components;
using Blazor.Shared.Services;
using Blazor.Shared.UserManagement.Services;
using Core.Shared.Messaging;
using Core.Shared.UserManagement.Contracts;
using Core.UiHosting;
using DevExpress.Blazor;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdk.Backend.Extensions;
using Sdk.Backend.Modules;
using Sdk.Client.Modules;
using Sdk.Extensions;
using Sdk.Modules;

namespace Blazor.Server.Backend;

public sealed class BlazorServerBackendModule : BackendModule, IUiHostModule
{
    private readonly UiModuleManager _moduleHost = new();

    public void LoadUiDependencies(IServiceCollection services, IUiHostEnvironment uiEnvironment)
    {
        var moduleBundles = uiEnvironment.LoadModuleBundles(CreateBundle);
        _moduleHost.AddModuleBundles(moduleBundles);

        services.AddSingleton(_moduleHost);
        services.AddSingleton<IUiModuleManager>(s => s.GetRequiredService<UiModuleManager>());
    }

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

        // The ITicketStore is implemented in Core.OS because it needs to access db context or other core parts
        services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>>(
            sp => new CookieAuthConfigurator(sp.GetRequiredService<ITicketStore>()));

        services.AddSingleton<IAppRenderingProvider, ServerRenderingProvider>();
        services.AddDevExpressBlazor(configure => configure.BootstrapVersion = BootstrapVersion.v5);
        services.AddTransient(typeof(IUiEventPublisher<>), typeof(UiEventPublisher<>));
        services.AddTransient(typeof(IUiEventSubscriptionHolder<>), typeof(UiEventPublisher<>));
        services.AddSingleton(typeof(IUiEventSubscriptionRegistry<>), typeof(UiEventSubscriptionRegistry<>));
        services.AddServerHttpClient();

        services.AddScoped<ILanguageCookieReader, LanguageCookieReader>();
    }
    
    public void ConfigureUiServices(IServiceCollection services, IUiHostEnvironment uiEnvironment, Action<string, Exception>? errorOccured = null)
    {
        var failedModuleIds = new List<string>();

        services.AddUiHostClientServices();

        // we need to register the client modules services
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
                errorOccured?.Invoke(module.ModuleId, e);
                failedModuleIds.Add(module.ModuleId);
            }
        }

        // remove failed ui bundles from internal manager
        foreach (var moduleId in failedModuleIds)
            _moduleHost.RemoveUiModuleBundle(moduleId);
    }

    public void UseUiHost(IApplicationBuilder app, IWebHostEnvironment env, IUiHostEnvironment uiEnvironment)
    {
        var logger = GetLogger(app.ApplicationServices);
        if (uiEnvironment.IsDevelopment)
        {
            env.UseClientAssetsDevelopment(uiEnvironment, logger);
        }
        else
        {
            // this works for published client e.g. @"path\to\publish\Client\wwwroot"
            env.UseClientAssetsProduction(uiEnvironment, logger);
        }

        var options = new DefaultFilesOptions();
        options.DefaultFileNames.Add("static-restart.html");

        app.UseDefaultFiles(options);

        // include the files in the wwwroot folder as assets
        app.UseStaticFiles();
    }

    private IUiModuleBundle CreateBundle(string assemblyPath, Assembly assembly)
    {
        var modules = assembly.GetInstances<ClientModule>().ToList();
        if (modules.Count > 1)
            throw new InvalidOperationException("Multiple modules defined within assembly. Only one is allowed!");

        return new UiModuleBundle(modules.First(), assemblyPath, assembly);
    }

    public void ConfigureIdentity(IdentityBuilder builder) { }

    public void UseSecurity(IApplicationBuilder app, bool useHeaderForwarding)
    {
        if (useHeaderForwarding)
        {
            var forwardedHeaderOptions = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto };
            forwardedHeaderOptions.KnownIPNetworks.Clear();
            forwardedHeaderOptions.KnownProxies.Clear();

            app.UseForwardedHeaders(forwardedHeaderOptions);
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();

        app.UseRequestLocalization(new RequestLocalizationOptions
        {
            DefaultRequestCulture = new(Shared.Constants.SupportedCultures.First()),
            SupportedCultures = Shared.Constants.SupportedCultures,
            SupportedUICultures = Shared.Constants.SupportedCultures,
            RequestCultureProviders = [new CookieRequestCultureProvider()]
        });

        app.UseMiddleware<OnboardingMiddleware>();
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var logger = GetLogger(endpoints.ServiceProvider);

        // map endpoints for e.g. DebugController
        endpoints.MapControllers();

        endpoints.MapExternalIdentityEndpoints();

        // add authentication pages /Account/*
        // https://andrewlock.net/should-you-use-the-dotnet-8-identity-api-endpoints/
        // ...endpoints.MapAdditionalIdentityEndpoints();
        endpoints.MapRazorPages();

        logger.LogInformation("Mapping endpoints for {Modules}", string.Join(", ", _moduleHost.UiModules.Select(k => k.ModuleId)));

        // map blazor endpoints blazor.web.js, / etc.
        endpoints.MapRazorComponents<App>()
            // we need to add the blazor.backend and module routes so router can process them on deep link
            .AddAdditionalAssemblies(_moduleHost.GetAdditionalAssemblies())
            // here we could add also support for the wasm render mode if we merge the hosts
            .AddInteractiveServerRenderMode();
    }

    private static ILogger GetLogger(IServiceProvider provider)
        => provider.GetRequiredService<ILogger<BlazorServerBackendModule>>();

    private class UiModuleBundle(ClientModule module, string assemblyLocation, Assembly? assembly) : IUiModuleBundle
    {
        public IModule Module { get; } = module;
        public string AssemblyLocation { get; } = assemblyLocation;
        public Assembly? Assembly { get; } = assembly;
    }
}
