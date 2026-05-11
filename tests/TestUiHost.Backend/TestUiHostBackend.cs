using Core.UiHosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Backend.Modules;
using Sdk.Instance;
using TestUiHost.Services;

namespace TestUiHost;

public class TestUiHostBackend : BackendModule, IUiHostModule
{
    public const string Id = "ViciOne.Suite.Test.UiHost";
    public const string AssemblyName = Id + ".Backend";

    private string? _defaultCulture;

    public event EventHandler<string>? CallReceived;

    public override void ConfigureServices(IServiceCollection services, IConfiguration config, IMvcBuilder builder)
    {
        services.AddSingleton<IInstanceInformationProvider, TestInstanceInformationProvider>();
        CallReceived?.Invoke(this, nameof(ConfigureServices));
    }

    public void ConfigureIdentity(IdentityBuilder builder) =>
        CallReceived?.Invoke(this, nameof(ConfigureIdentity));

    public void ConfigureUiServices(IServiceCollection services, IUiHostEnvironment uiEnvironment, Action<string, Exception>? errorOccurred = null)
        => CallReceived?.Invoke(this, nameof(ConfigureUiServices));

    public void UseSecurity(IApplicationBuilder app, bool useHeaderForwarding)
        => CallReceived?.Invoke(this, nameof(UseSecurity));

    public void LoadUiDependencies(IServiceCollection services, IUiHostEnvironment uiEnvironment)
        => CallReceived?.Invoke(this, nameof(LoadUiDependencies));

    public void UseUiHost(IApplicationBuilder app, IWebHostEnvironment env, IUiHostEnvironment uiEnvironment)
        => CallReceived?.Invoke(this, nameof(UseUiHost));
    public static string GetAssemblyName() => $"{typeof(TestUiHostBackend).Assembly!.GetName()!.Name}.dll";
    public void SetDefaultRequestCulture(string? cultureName) => _defaultCulture = cultureName;
    public string? GetDefaultRequestCulture() => _defaultCulture;
}
