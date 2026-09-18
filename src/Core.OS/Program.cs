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

var instanceOptions = builder.Configuration.GetInstanceOptions();

// the logger stamps the instance id on every exported event, so the device identity has to exist
// before it is created - otherwise the logs of a first boot report no instance while the traces do.
// The preparation pipeline repeats this call; writing the file is idempotent.
fileSystem.EnsureInstanceIdFile(instanceOptions);

// add ILogLevelSwitch and configure serilogs sinks etc.
builder.Services.ConfigureLogging(builder.Configuration, fileSystem, instanceOptions);

// add serilogs services like DiagnosticContext
builder.Host.UseSerilog();

using var loggerFactory = new SerilogLoggerFactory(logger: null, dispose: false);
var preparationContext = new SuitePreparationContext(fileSystem, instanceOptions, loggerFactory);
using var preparationPipeline = new SuitePreparationPipeline(preparationContext);

// filesystem / workspace preparation
var result = await preparationPipeline
    .UseEnvironmentOverridesLogging(environmentOverridesPath, environmentOverridesFailure)
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
