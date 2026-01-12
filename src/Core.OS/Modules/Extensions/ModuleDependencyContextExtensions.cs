using Core.Module;
using Core.Shared.Modules;
using Sdk.Messaging;
using Sdk.Modules;

namespace Core.OS.Modules.Extensions;

internal static class ModuleDependencyContextExtensions
{
    public static List<ErrorInfo> GetErrorInfos(this ModuleDependencyContext context)
        => context.StartupErrors
        .Select(k => new ErrorInfo(
            context.ModuleType == ModuleType.Backend ? ModuleErrorCodes.StartupError : ModuleErrorCodes.StartupErrorUi,
            k.Message))
        .ToList();
}
