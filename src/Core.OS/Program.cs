using System.IO.Abstractions;
using Core.OS.EnvironmentOverrides;
using Core.OS.Extensions;
using Core.OS.Hosting;
using Core.OS.Hosting.Extensions;
using Core.OS.Instance.Extensions;
using Core.OS.Logging;
using Serilog;
using Serilog.Extensions.Logging;

Thread.CurrentThread.Name = "MainThread";

var fileSystem = new FileSystem();

// Apply runtime env-var overrides into the process environment before the host builder is
// created, so both the .NET options pipeline and external libs (e.g., OTEL) honor them.
// Reported below, once logging is configured.
var environmentOverridesPath = EnvironmentOverridesStartup.ResolvePath(fileSystem);
var environmentOverridesFailure = await EnvironmentOverridesLoader.Apply(fileSystem, environmentOverridesPath);

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

if (environmentOverridesFailure is not null)
    Log.Error(environmentOverridesFailure,
        "Environment overrides were not applied - continuing with the inherited environment");
else if (environmentOverridesPath is null)
    Log.Information(
        "Runtime environment overrides are off - set {Variable} to true to enable them",
        EnvironmentOverridesFile.EnabledEnvironmentVariable);

var instanceOptions = builder.Configuration.GetInstanceOptions();
using var loggerFactory = new SerilogLoggerFactory(logger: null, dispose: false);
var preparationContext = new SuitePreparationContext(fileSystem, instanceOptions, loggerFactory);
using var preparationPipeline = new SuitePreparationPipeline(preparationContext);

// filesystem / workspace preparation
var result = await preparationPipeline
    .UseInstanceIdentification()
    .UseDeviceImageCleanup()
    .UseResetIfRequested()
    .UseRestoreIfRequested()
    .UseVersionDowngradeDetection()
    .UseRecoveryMode(builder)
    .UseModulePreparation(builder)
    .RunWithProcessSignalsAsync();

// build host and validate options
await builder.TryRunCoreOs(preparationContext, result, args);
