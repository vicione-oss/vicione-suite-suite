using Blazor.Wasm.Client.Services;
using Xunit;

namespace Blazor.Wasm.Tests.Client.Services;

public sealed class ClientModuleServiceTests
{
    private readonly ClientModuleService _service = new();

    [Fact]
    public void GetModules()
    {
        var modules = _service.GetModules();

        Assert.NotNull(modules);
        Assert.NotEmpty(modules);
    }

    [Fact]
    public void GetModuleByType()
    {
        var modules = _service.GetModules();

        var first = modules.First();
        var module = ClientModuleService.GetModuleByType(first.GetType());

        Assert.NotNull(module);
        Assert.Equal(module.ModuleId, first.ModuleId);
    }
}
