using Blazor.Shared.SystemInformation.Models;
using ViciOne.SystemMonitoring.Collectors;

namespace Blazor.Shared.SystemInformation.Services;

internal static class ProcessRoles
{
    public static SuiteProcessRole Of(LinuxProcessInfo process)
        => process.Pid == Environment.ProcessId
            ? SuiteProcessRole.Suite
            : process.CmdLine?.Contains("EngineHost", StringComparison.OrdinalIgnoreCase) == true
                ? SuiteProcessRole.EngineHost
                : SuiteProcessRole.None;
}
