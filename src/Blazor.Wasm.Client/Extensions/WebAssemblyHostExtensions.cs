using System.Globalization;
using Blazor.Wasm.Client.Infrastructure.Modules;
using Blazor.Wasm.Client.Infrastructure.SignalR;
using Blazor.Wasm.Client.Services;
using Core.Shared.Instance.Contracts;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ViciOne.Ui.Localization.Extensions;

namespace Blazor.Wasm.Client.Extensions;

public static class WebAssemblyHostExtensions
{
    /// <summary>
    /// load module infos from unauthorized backend controller and set it statically in client module service
    /// to skip loading disabled/unavailable assemblies (still referenced)
    /// later we'll have to load also the module dlls before the startup
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static async Task LoadModuleAssemblies(this WebAssemblyHostBuilder builder)
    {
        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
        var moduleClient = new BackendModuleHttpClient(httpClient);

        var result = await ClientModuleLoader.LoadClientAssemblies(moduleClient);

        builder.HostEnvironment.LogInDevelopment(result, "Loaded client modules");
    }

    public static async Task SetLocalInstanceInformation(this WebAssemblyHost host)
    {
        var environment = host.Services.GetRequiredService<IWebAssemblyHostEnvironment>();

        // can't use client from service collection because it has no valid access token here
        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(environment.BaseAddress);
        var moduleClient = new BackendModuleHttpClient(httpClient);

        var moduleInfos = await moduleClient.GetModuleMetadata();

        ClientModuleService.SetBackendModuleInfos(moduleInfos);

        await ClientInstanceInformationProvider.SetLocalInstanceInfo(moduleClient);
    }

    public static async Task StartSignalRConnection(this WebAssemblyHost host)
    {
        var messageHub = host.Services.GetRequiredService<IClientMessageHub>();
        await messageHub.Start();
    }

    public static async Task SetCulture(this WebAssemblyHost host)
    {
        const string defaultCulture = CrossInstanceConfiguration.CultureNameDefault;

        var culture = new CultureInfo(defaultCulture);

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        // try to load localization assemblies only if it's not the default language
        if (!defaultCulture.StartsWith(culture.ThreeLetterISOLanguageName, StringComparison.Ordinal))
        {
            var environment = host.Services.GetRequiredService<IWebAssemblyHostEnvironment>();
            environment.LogInDevelopment($"Load culture {culture.Name} ({culture.TwoLetterISOLanguageName})");

            var moduleClient = host.Services.GetRequiredService<IBackendModuleHttpClient>();
            var result = await ClientModuleLoader.LoadClientResourceAssemblies(moduleClient, CultureInfo.DefaultThreadCurrentCulture);

            environment.LogInDevelopment(result, "Loaded client module resources");
        }
    }

    private static void LogInDevelopment(this IWebAssemblyHostEnvironment env, ClientModuleLoaderResult result, string prefix)
    {
        if (!env.IsSuiteDevelopment())
            return;

        if (result.Error is not null)
        {
            Console.WriteLine("Failed to load client modules: {0}", result.Error.Message);
        }

        Console.WriteLine("{0}. loaded dlls:{1}/{2} (errors:{3}) zip:{4}",
            prefix,
            result.LoadedDlls.Count,
            result.UnzippedDllCount,
            result.LoadErrors.Count,
            result.ZipLength.LocalizeFileSizeHumanReadable());

        if (result.LoadErrors.Count > 0)
        {
            Console.WriteLine("Failed to load: {0}", string.Join(",", result.LoadErrors));
        }
    }

    public static void LogInDevelopment(this IWebAssemblyHostEnvironment env, string message)
    {
        if (!env.IsSuiteDevelopment())
            return;

        Console.WriteLine(message);
    }

    private static bool IsSuiteDevelopment(this IWebAssemblyHostEnvironment hostingEnvironment)
        => hostingEnvironment.Environment.StartsWith("Development", StringComparison.Ordinal);
}
