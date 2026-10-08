using System.Net;
using Core.OS.HostManagement;
using HostManagement.Shared.Capabilities;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Network;
using HostManagement.Shared.Contracts.Service;
using HostManagement.Shared.Enums;

namespace Core.OS.Tests.HostManagement;

public sealed class DisabledSettingsTests
{
    private readonly SystemConfiguration _current = TestPipeClient.GetEmbeddedSystemConfiguration();
    private readonly SystemConfiguration _proposed = TestPipeClient.GetEmbeddedSystemConfiguration();
    private readonly SupportedSettings _allDisabled = new();

    public static TheoryData<string, Action<SystemConfiguration>> LeafChanges => new()
    {
        { "IPv4", configuration => configuration.NetworkInterfaces[0].IPv4.DHCPEnabled = false },
        { "VLAN", configuration => configuration.NetworkInterfaces[0].VLAN = new VLANSettings { Enabled = true, ID = 5 } },
        { "Hostname", configuration => configuration.NetworkDNSSettings.Hostname = "other-host" },
        { "MulticastDNS", configuration => configuration.NetworkDNSSettings.MulticastDNSEnabled = !configuration.NetworkDNSSettings.MulticastDNSEnabled },
        { "StaticHosts", configuration => configuration.NetworkDNSSettings.StaticHosts.Enabled = true },
        { "NameServers", configuration => configuration.NetworkDNSSettings.NameServers.Addresses.Add(IPAddress.Parse("8.8.8.8")) },
        { "PrimaryDNSSuffix", configuration => configuration.NetworkDNSSettings.PrimaryDNSSuffix.Suffix = "example.com" },
        { "SearchDomains", configuration => configuration.NetworkDNSSettings.SearchDomains.Domains.Add("example.com") },
        { "Proxy", configuration => configuration.NetworkProxySettings.HTTP = new NetworkProxyDetail { Enabled = true, Server = "proxy", Port = 8080 } },
        { "NTP", configuration => configuration.NetworkNTPSettings.Servers.Add("time.example.com") },
        { "Services", configuration => configuration.Services.Add(new ServiceDetail { Name = "module", State = ServiceState.Enabled }) },
    };

    [Theory]
    [MemberData(nameof(LeafChanges))]
    public void Should_report_a_changed_disabled_setting(string settingName, Action<SystemConfiguration> change)
    {
        // Arrange
        change(_proposed);

        // Act
        var result = DisabledSettings.ChangedBy(_current, _proposed, _allDisabled);

        // Assert
        result.Should().Equal(settingName);
    }

    [Fact]
    public void Should_not_report_unchanged_settings()
    {
        // Act
        var result = DisabledSettings.ChangedBy(_current, _proposed, _allDisabled);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Should_not_report_a_changed_enabled_setting()
    {
        // Arrange
        var settings = new SupportedSettings();
        settings.DNS.Hostname.Capability = CapabilityStatus.Enabled;
        _proposed.NetworkDNSSettings.Hostname = "other-host";

        // Act
        var result = DisabledSettings.ChangedBy(_current, _proposed, settings);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Should_report_ipv4_and_vlan_when_an_interface_is_added()
    {
        // Arrange
        _proposed.NetworkInterfaces.Add(_proposed.NetworkInterfaces[0] with
        {
            CommonInformation = _proposed.NetworkInterfaces[0].CommonInformation with { Name = "lan3" }
        });

        // Act
        var result = DisabledSettings.ChangedBy(_current, _proposed, _allDisabled);

        // Assert
        result.Should().Equal("IPv4", "VLAN");
    }

    [Fact]
    public void Should_report_ipv4_and_vlan_when_an_interface_is_removed()
    {
        // Arrange
        _proposed.NetworkInterfaces.RemoveAt(0);

        // Act
        var result = DisabledSettings.ChangedBy(_current, _proposed, _allDisabled);

        // Assert
        result.Should().Equal("IPv4", "VLAN");
    }

    [Fact]
    public void Should_match_interfaces_by_name()
    {
        // Arrange
        _proposed.NetworkInterfaces.Reverse();

        // Act
        var result = DisabledSettings.ChangedBy(_current, _proposed, _allDisabled);

        // Assert
        result.Should().BeEmpty();
    }
}
