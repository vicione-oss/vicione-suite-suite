using Blazor.DevAssets;
using Blazor.Shared.Components;
using Blazor.Shared.Services;
using Blazor.Wasm.Backend.Controllers;
using Blazor.Wasm.Backend.SignalR;
using Blazor.Wasm.Client;
using Blazor.Wasm.Client.Infrastructure.SignalR;
using Core.Shared.Messaging;
using Core.UiHosting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Extensions;
using Sdk.Backend.Modules;

namespace Blazor.Wasm.Backend;

public sealed class BlazorWasmBackendModule : BackendModule, IUiHostModule
{
    public void LoadUiDependencies(IServiceCollection services, IUiHostEnvironment uiEnvironment)
    {
        // not used in blazor wasm
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration config, IMvcBuilder builder)
    {
        var options = config.BindSection<UiHostCircuitOptions>(ModuleId);
        services.AddAntiforgery();
        services.AddRazorPages();
        services.AddRazorComponents()
            .AddInteractiveWebAssemblyComponents();

        services.AddSignalR(o =>
        {
            o.EnableDetailedErrors = options.EnableDetailedErrors;
            o.MaximumReceiveMessageSize = options.MaximumReceiveMessageSize; // no limit if null!!
        });
        services.AddResponseCompression(opts =>
        {
            opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                ["application/octet-stream"]);
        });
        services.AddTransient<IMessageHub, MessageHub>();
        services.AddTransient(typeof(IUiEventPublisher<>), typeof(UiEventToSignalRPublisher<>));
        services.AddSingleton<IAppRenderingProvider, WasmRenderingProvider>();

        services.AddCors(opt =>
        {
            opt.AddPolicy("CorsSpecs",
                b =>
                {
                    b
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .SetIsOriginAllowed(_ => true)
                        .AllowCredentials();
                });
        });

        // work around to be able to test controllers using cookies 
        services.AddTransient<ICookieAccessor, CookieAccessor>();
        services.ConfigureApplicationCookie(o =>
        {
            o.Cookie.HttpOnly = true;
            o.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = 401;
                return Task.CompletedTask;
            };
        });

        services.AddAuthentication(
                CookieAuthenticationDefaults.AuthenticationScheme
            )
            .AddCookie();

        services.AddAuthorization();
    }

    public void ConfigureIdentity(IdentityBuilder builder) { }

    public void ConfigureUiServices(IServiceCollection services, IUiHostEnvironment uiEnvironment, Action<string, Exception>? errorOccured = null)
    {
        // no ui modules loaded in backend!
    }

    public void UseUiHost(IApplicationBuilder app, IWebHostEnvironment env, IUiHostEnvironment uiEnvironment)
    {
        app.UseResponseCompression();

        var logger = app.ApplicationServices.GetRequiredService<ILogger<BlazorWasmBackendModule>>();
        if (uiEnvironment.IsDevelopment)
        {
            // requires Backend to reference the Microsoft.AspNetCore.Components.WebAssembly.Server package                
            // to provide the BlazorDebugProxy in the expected location. Maybe the extension could be overwritten
            // to use UiCoreModule path instead
            app.UseWebAssemblyDebugging();
            env.UseClientAssetsDevelopment(uiEnvironment, logger);
        }
        else
        {
            // this works for published client e.g. @"path\to\publish\Client\wwwroot"
            env.UseClientAssetsProduction(uiEnvironment, logger);
        }

        app.UseCors("CorsSpecs");
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapRazorPages();
        endpoints.MapControllers();
        endpoints.MapHub<MessageHub>(Shared.Constants.SignalRHubEndpoint);
        endpoints.MapRazorComponents<App>()
            // this is quite weird now because we register the routing in the
            // backend instead of wasm program.cs
            // We don't have the client assemblies loaded in wasm backend and therefore
            // it's not possible for now to add them here easily. To show that deeplinking
            // is working with registered assembly routes here try: https://suite/logging
            .AddAdditionalAssemblies(typeof(BlazorWasmClientModule).Assembly)
            .AddInteractiveWebAssemblyRenderMode();

        // include the files in the wwwroot folder as assets
        endpoints.MapStaticAssets();
    }

    public void UseSecurity(IApplicationBuilder app, bool useHeaderForwarding)
    {
        if (useHeaderForwarding)
        {
            var forwardedHeaderOptions = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto };
            forwardedHeaderOptions.KnownNetworks.Clear();
            forwardedHeaderOptions.KnownProxies.Clear();

            app.UseForwardedHeaders(forwardedHeaderOptions);
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();
    }
}
