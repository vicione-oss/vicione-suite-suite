using Sdk.Client.Modules;
using Sdk.Modules;

namespace Blazor.Shared.Tests.Models;

internal sealed class TestClientModuleA : IClientModule
{
    public const string ModuleId = nameof(TestClientModuleA);

    public ModuleKey ModuleKey => new() { ModuleId = ModuleId };

    public IEnumerable<ModuleKey> Dependencies => [];
}
