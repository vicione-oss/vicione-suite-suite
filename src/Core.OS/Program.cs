using System.IO.Abstractions;
using Core.OS.Extensions;
using Core.OS.Hosting;
using Core.OS.Hosting.Extensions;
using Core.OS.Instance.Extensions;
using Core.OS.Logging;
using Core.OS.Modules.Extensions;
using Serilog;
using Serilog.Extensions.Logging;

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
using var loggerFactory = new SerilogLoggerFactory(logger: null, dispose: false);

// on preparation we access http api, load modules etc.
var result = await new SuitePreparationPipeline(loggerFactory.CreateLogger<SuitePreparationPipeline>())
    // filesystem / workspace preparation
    .UseInstanceId(fileSystem, instanceOptions)
    .UseDeviceImageCleanup(fileSystem, instanceOptions)
    .UseResetFile(fileSystem, instanceOptions)
    .UseRestore(fileSystem, instanceOptions)
    .UseVersionDowngradeCheck(fileSystem, instanceOptions)
    .UseRecoveryMode(builder, fileSystem, instanceOptions)
    // host / DI setup — only reached when all previous preparation steps succeed
    .Use(async (logger, ct) =>
    {
        // add serilogs services like DiagnosticContext 
        builder.Host.UseSerilog();

        // validate appsettings, env vars etc.
        builder.Services.ConfigureAndValidateOptions(instanceOptions);

        builder.Services.AddSuiteOpenTelemetry(builder.Configuration, instanceOptions, fileSystem);

        // modules need to be (down-)loaded before server starts
        var moduleHost = await builder.AddModuleHost(fileSystem, instanceOptions, loggerFactory, ct);

        builder.Services.AddServices(fileSystem, builder.Configuration, moduleHost);
    })
    .RunWithProcessSignalsAsync();


// build host and validate options
await builder.TryRunCoreOs(fileSystem, result, args);
