using System.Net;
using Blazor.Shared.Network.Extensions;
using Blazor.Shared.Services;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Network;

namespace Blazor.Shared.Tests.Network.Extensions;

public sealed class ISystemConfigurationServiceExtensionsTests
{
    private readonly ISystemConfigurationService _systemConfigurationService = Substitute.For<ISystemConfigurationService>();

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void Should_return_the_static_dns_name_servers_only_while_they_are_enabled(bool enabled, int expectedCount)
    {
        // Arrange
        _systemConfigurationService.SystemConfiguration.Returns(new SystemConfiguration
        {
            NetworkDNSSettings = new NetworkDNSSettings
            {
                NameServers = new() { Enabled = enabled, Addresses = [IPAddress.Parse("8.8.8.8")] }
            }
        });

        // Act
        var nameServers = _systemConfigurationService.GetStaticDnsNameServers();

        // Assert
        nameServers.Should().HaveCount(expectedCount);
    }
}
