using System.IO.Abstractions;
using Core.OS.Extensions;
using Core.OS.Hosting;
using Core.OS.Hosting.Extensions;
using Core.OS.Instance.Extensions;
using Core.OS.Logging;
using Serilog;
using Serilog.Extensions.Logging;

Thread.CurrentThread.Name = "MainThread";

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<HostOptions>(c =>
{
    c.ServicesStartConcurrently = true;
    c.ServicesStopConcurrently = true;
});

// add ILogLevelSwitch and configure serilogs sinks etc.
builder.Services.ConfigureLogging(builder.Configuration);

// add serilogs services like DiagnosticContext
builder.Host.UseSerilog();

var fileSystem = new FileSystem();
var instanceOptions = builder.Configuration.GetInstanceOptions();
using var loggerFactory = new SerilogLoggerFactory(logger: null, dispose: false);
var preparationContext = new SuitePreparationContext(fileSystem, instanceOptions, loggerFactory);

// filesystem / workspace preparation
var result = await new SuitePreparationPipeline(preparationContext)
    .UseInstanceId()
    .UseDeviceImageCleanup()
    .UseResetFile()
    .UseRestore()
    .UseVersionDowngradeCheck()
    .UseRecoveryMode(builder)
    .UseModulePipeline(builder)
    .RunWithProcessSignalsAsync();

// build host and validate options
await builder.TryRunCoreOs(preparationContext, result, args);
