using Blazor.Wasm.Client.Extensions;
using Blazor.Wasm.Client.Infrastructure;
using Blazor.Wasm.Client.Infrastructure.Security;
using Blazor.Wasm.Client.Services;
using DevExpress.Blazor;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

try
{
    var builder = WebAssemblyHostBuilder.CreateDefault(args);

    await builder.LoadModuleAssemblies();

    builder.Services
        .AddLocalization()
        .AddInfrastructure(builder.HostEnvironment.BaseAddress)
        .AddClientServices()
        .AddSecurity()
        .AddCompatibleHostEnvironment()
        .AddDevExpressBlazor(configure => configure.BootstrapVersion = BootstrapVersion.v5);

    ClientModuleService.RegisterModuleServices(builder);

    var host = builder.Build();

    await host.SetCulture();

    await host.SetLocalInstanceInformation();

    await host.StartSignalRConnection();

    await host.RunAsync();
}
catch (Exception ex)
{
    // ILogger is available after startup but only messages >= Information will be logged
    // https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/logging?view=aspnetcore-6.0

    Console.WriteLine("An exception occurred while creating the WASM host");
    Console.WriteLine(ex);
    throw;
}
