using System.IO.Abstractions;
using Core.OS;
using Core.OS.Extensions;
using Core.OS.Instance.Extensions;
using Core.OS.Logging;
using Core.OS.Modules.Extensions;
using Serilog;

Thread.CurrentThread.Name = "MainThread";

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<HostOptions>(c =>
{
    c.ServicesStartConcurrently = true;
    c.ServicesStopConcurrently = true;
});

// this one is used to log on startup before service provider is ready
LoggingConfiguration.SetupStaticStartupLogger(builder.Configuration);

// on preparation we access http api, load modules etc.
await CancelableAppPreparation.Execute(async (cancellationToken) =>
{
    var instanceOptions = builder.Configuration.GetInstanceOptions();
    var fileSystem = new FileSystem();

    if (!await builder.PrepareSuite(fileSystem, instanceOptions, Log.Logger, cancellationToken))
    {
        // if preparation fails suite startup ends here    
        return;
    }

    // configure serilog
    builder.Host.ConfigureLogging();

    // validate appsettings, env vars etc.
    builder.Services.ConfigureAndValidateOptions(instanceOptions);

    builder.Services.AddSuiteOpenTelemetry(builder.Configuration, instanceOptions, fileSystem);

    // modules need to be (down-)loaded before server starts
    var moduleHost = await builder.AddModuleHost(fileSystem, instanceOptions, cancellationToken);

    builder.Services.AddServices(fileSystem, builder.Configuration, moduleHost);
});

// build host and validate options
await builder.TryRunCoreOs(args);
