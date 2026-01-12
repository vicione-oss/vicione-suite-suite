using ViciOne.SystemMonitoring.Collectors;

namespace Blazor.Shared.SystemInformation.Extensions;

internal static class LinuxProcessInfoExtentions
{
    internal static bool CmdLineContains(this LinuxProcessInfo info, string value)
    {
        if (string.IsNullOrEmpty(info.CmdLine))
            return false;

        return info.CmdLine.Contains(value, StringComparison.OrdinalIgnoreCase);
    }
}
