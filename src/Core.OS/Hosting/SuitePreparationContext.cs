using System.IO.Abstractions;
using Core.OS.Instance;
using Core.OS.Modules.Hosting;

namespace Core.OS.Hosting;

internal sealed class SuitePreparationContext(IFileSystem fileSystem, InstanceOptions instanceOptions, ILoggerFactory loggerFactory)
{
    private readonly ILogger logger = loggerFactory.CreateLogger(nameof(SuitePreparationContext));

    public IFileSystem FileSystem => fileSystem;

    public ILogger Logger => logger;

    public ILoggerFactory LoggerFactory => loggerFactory;

    public InstanceOptions InstanceOptions => instanceOptions;

    public ModulePreparationContext? ModuleContext { get; set; }
}
