using Core.Module.Options;

namespace Core.Module.Tests;

internal static class SystemTestSettings
{
    public static ModuleApiOptions ModuleApiOptions = new()
    {
        Endpoint = "https://ifm.jfrog.io/artifactory/vicione-suite",
        UserName = "",
        Password = "",
    };
}
