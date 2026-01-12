using System.IO.Abstractions;
using Core.OS;
using Core.OS.Extensions;
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

    builder.Host.ConfigureLogging();

    // modules need to be (down-)loaded before server starts
    builder.Services.ConfigureAndValidateOptions(instanceOptions);
    var moduleHost = await builder.AddModuleSupport(fileSystem, instanceOptions, cancellationToken);

    builder.Services.AddPlatformServices(fileSystem, builder.Configuration, moduleHost);
    builder.Services.AddSystemMonitoring(builder.Configuration);
    builder.Services.AddJournalService();
});

// build host and validate options
await builder.TryRunSuite(args);
