using System.Net;
using Core.OS.Instance;
using IPNetwork = System.Net.IPNetwork;

namespace Core.OS.Tests.Instance;

public class TrustedProxySafelistTests
{
    public class Parse : TrustedProxySafelistTests
    {
        [Fact]
        public void Should_return_empty_safelist_for_no_entries()
        {
            // Act
            var safelist = TrustedProxySafelist.Parse([]);

            // Assert
            safelist.Proxies.Should().BeEmpty();
            safelist.Networks.Should().BeEmpty();
        }

        [Fact]
        public void Should_parse_an_ip_address_as_a_proxy()
        {
            // Act
            var safelist = TrustedProxySafelist.Parse(["203.0.113.7"]);

            // Assert
            safelist.Proxies.Should().ContainSingle().Which.Should().Be(IPAddress.Parse("203.0.113.7"));
            safelist.Networks.Should().BeEmpty();
        }

        [Fact]
        public void Should_parse_a_cidr_entry_as_a_network()
        {
            // Act
            var safelist = TrustedProxySafelist.Parse(["10.0.0.0/24"]);

            // Assert
            safelist.Networks.Should().ContainSingle().Which.Should().Be(IPNetwork.Parse("10.0.0.0/24"));
            safelist.Proxies.Should().BeEmpty();
        }

        [Fact]
        public void Should_split_mixed_entries_into_proxies_and_networks()
        {
            // Act
            var safelist = TrustedProxySafelist.Parse(["203.0.113.7", "10.0.0.0/24", "2001:db8::1"]);

            // Assert
            safelist.Proxies.Should().BeEquivalentTo([IPAddress.Parse("203.0.113.7"), IPAddress.Parse("2001:db8::1")]);
            safelist.Networks.Should().ContainSingle().Which.Should().Be(IPNetwork.Parse("10.0.0.0/24"));
        }

        [Fact]
        public void Should_reject_an_entry_that_is_neither_ip_address_nor_cidr_network()
        {
            // Act
            var act = () => TrustedProxySafelist.Parse(["proxy.example.com"]);

            // Assert
            act.Should().Throw<InvalidOperationException>().WithMessage("*proxy.example.com*");
        }
    }
}
