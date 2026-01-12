using Blazor.Wasm.Client.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blazor.Wasm.Tests.Client.Infrastructure;

public class ConfigurationTests
{
    [Fact]
    public void Add_infrastructure_configuration()
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddInfrastructure("localhost");
    }
}
