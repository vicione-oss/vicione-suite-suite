using Core.OS.Hosting;
using Core.OS.Hosting.Contracts;

namespace Core.OS.Modules.Hosting;

/// <summary>
/// Builds and executes an ordered sequence of module-host preparation steps over a shared
/// <see cref="ModulePreparationContext"/>. Execution stops at the first
/// <see cref="IPreparationAbortResult"/>.
/// </summary>
internal sealed class ModulePreparationPipeline(ModulePreparationContext context)
    : PreparationPipeline<ModulePreparationPipeline, ModulePreparationContext>(context)
{
    protected override ILogger Logger => Context.Logger;

    protected override IPreparationAbortResult CreateFaultResult(string stepName, Exception exception)
        => new ModuleHostPreparationResult($"Module preparation step '{stepName}' failed: {exception.Message}");
}
