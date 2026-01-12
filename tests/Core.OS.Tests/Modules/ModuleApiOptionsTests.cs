using Core.OS.Extensions;
using Core.OS.Tests.Extensions;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Core.OS.Tests.Modules;

public class ModuleApiOptionsTests
{
    [Fact]
    public void Configuration_should_work()
    {
        // Arrange
        var builder = new ConfigurationBuilder();
        builder.AddCoreAppSettings("Development");

        // Act
        var apiOptions = builder.Build().GetModuleApiOptions();

        // Assert
        apiOptions.Endpoint.Should().NotBeNull().And.NotBeEmpty();
    }
}
