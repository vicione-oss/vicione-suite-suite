using System.IO.Abstractions;
using Core.OS.EnvironmentOverrides;
using Core.Shared.EnvironmentOverrides;
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

var instanceOptions = builder.Configuration.GetInstanceOptions();

// the logger stamps the instance id on every exported event, so the device identity has to exist
// before it is created - otherwise the logs of a first boot report no instance while the traces do.
// The preparation pipeline repeats this call; writing the file is idempotent.
fileSystem.EnsureInstanceIdFile(instanceOptions);

// add ILogLevelSwitch and configure serilogs sinks etc.
builder.Services.ConfigureLogging(builder.Configuration, fileSystem, instanceOptions);

// add serilogs services like DiagnosticContext
builder.Host.UseSerilog();

if (environmentOverridesFailure is not null)
    Log.Error(environmentOverridesFailure,
        "Environment overrides were not applied - continuing with the inherited environment");
else if (environmentOverridesPath is null)
    Log.Information(
        "Runtime environment overrides are off - set {Variable} to true to enable them",
        EnvironmentOverridesSwitch.EnabledEnvironmentVariable);

using var loggerFactory = new SerilogLoggerFactory(logger: null, dispose: false);

// An operator took the overrides away from the failsafe debug page. Reported so the journal of the
// boot that follows shows why this instance came back on the inherited environment.
var disabledOverridesPath = EnvironmentOverridesStartup.ResolveDisabledPath(fileSystem);
if (disabledOverridesPath is not null)
    loggerFactory.CreateLogger("Program").LogWarning(
        "Environment overrides were disabled and are not applied - the previous file is kept at {Path}",
        disabledOverridesPath);

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
