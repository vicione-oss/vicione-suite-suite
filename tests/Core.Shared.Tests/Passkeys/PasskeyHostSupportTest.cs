using Core.Shared.Passkeys;

namespace Core.Shared.Tests.Passkeys;

public class PasskeyHostSupportTest
{
    private readonly PasskeyHostSupport _sut = new();

    [Theory]
    [InlineData("localhost")]
    [InlineData("myhost")]
    [InlineData("edge.example.com")]
    [InlineData("sub.domain.co.uk")]
    public void Should_support_dns_hosts(string host)
        => Assert.True(_sut.IsPasskeyCapableHost(host));

    [Theory]
    [InlineData("10.0.0.5")]
    [InlineData("192.168.1.1")]
    [InlineData("::1")]
    [InlineData("[::1]")]
    [InlineData("fe80::1%eth0")]
    [InlineData("2001:db8::1")]
    public void Should_not_support_ip_literals(string host)
        => Assert.False(_sut.IsPasskeyCapableHost(host));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_not_support_empty_hosts(string? host)
        => Assert.False(_sut.IsPasskeyCapableHost(host));
}
