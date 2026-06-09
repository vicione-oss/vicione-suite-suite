using System.IO.Abstractions;
using Core.OS.Extensions;
using Core.OS.Hosting;
using Core.OS.Hosting.Extensions;
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

builder.Services.ConfigureLogging(builder.Configuration);
var fileSystem = new FileSystem();
var instanceOptions = builder.Configuration.GetInstanceOptions();

// on preparation we access http api, load modules etc.
var result = await new SuitePreparationPipeline()
    // filesystem / workspace preparation
    .UseInstanceId(fileSystem, instanceOptions)
    .UseDeviceImageCleanup(fileSystem, instanceOptions, Log.Logger)
    .UseResetFile(fileSystem, instanceOptions, Log.Logger)
    .UseRestore(fileSystem, instanceOptions, Log.Logger)
    .UseVersionDowngradeCheck(fileSystem, instanceOptions, Log.Logger)
    .UseRecoveryMode(builder, fileSystem, instanceOptions, Log.Logger)
    // host / DI setup — only reached when all previous preparation steps succeed
    .Use(async ct =>
    {
        builder.Host.UseSerilog();

        // validate appsettings, env vars etc.
        builder.Services.ConfigureAndValidateOptions(instanceOptions);

        builder.Services.AddSuiteOpenTelemetry(builder.Configuration, instanceOptions, fileSystem);

        // modules need to be (down-)loaded before server starts
        var moduleHost = await builder.AddModuleHost(fileSystem, instanceOptions, ct);

        builder.Services.AddServices(fileSystem, builder.Configuration, moduleHost);
    })
    .RunWithProcessSignalsAsync();


// build host and validate options
await builder.TryRunCoreOs(fileSystem, result, args);
