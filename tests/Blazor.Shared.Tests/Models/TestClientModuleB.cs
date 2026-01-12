using Sdk.Client.Modules;
using Sdk.Modules;

namespace Blazor.Shared.Tests.Models;

internal sealed class TestClientModuleB : IClientModule
{
    public const string ModuleId = nameof(TestClientModuleB);

    public ModuleKey ModuleKey => new() { ModuleId = ModuleId };

    public IEnumerable<ModuleKey> Dependencies => [];
}
