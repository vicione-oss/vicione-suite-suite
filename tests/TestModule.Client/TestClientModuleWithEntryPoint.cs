using Sdk.Client.Modules;

namespace TestModule.Client;

public sealed class TestClientModuleWithEntryPoint : ClientModule
{
    public const string ModuleRoute = "/test-client-module-with-entry-point";

    public const string Id = "TestClientModuleWithEntryPoint.Client";
}
