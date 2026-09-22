using Core.OS.UserManagement.Configuration;
using Core.OS.UserManagement.Security;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Core.OS.Tests.UserManagement.Security;

/// <summary>
/// The origin the Content Security Policy has to name so the challenge redirect survives
/// <c>form-action</c>, read from the very options the challenge itself is built from.
/// </summary>
public class ExternalLoginFormActionOriginTests
{
    [Theory]
    [InlineData("https://idp.example.com", "https://idp.example.com")]
    [InlineData("https://idp.example.com/realms/suite", "https://idp.example.com")]
    [InlineData("https://idp.example.com:8443/realms/suite", "https://idp.example.com:8443")]
    public void Should_name_the_origin_of_the_configured_authority(string authority, string expected)
        => Resolve(Configured(authority)).Should().Be(expected);

    /// <summary>
    /// The unconfigured case is a sentinel rather than an absent value, so a policy built from the
    /// authority alone would advertise <c>https://unconfigured.local</c> to every browser.
    /// </summary>
    [Fact]
    public void Should_name_no_origin_while_no_provider_is_configured()
    {
        var unconfigured = new OpenIdConnectOptions
        {
            Authority = "https://unconfigured.local",
            ClientId = Constants.UnconfiguredClient
        };

        Resolve(unconfigured).Should().BeNull();
    }

    [Fact]
    public void Should_name_no_origin_when_the_authority_is_the_suite_itself()
        => Resolve(Configured("https://suite.example.com/oidc")).Should().BeNull();

    [Fact]
    public void Should_name_no_origin_when_the_authority_is_not_a_usable_url()
        => Resolve(Configured("not-a-url")).Should().BeNull();

    /// <summary>
    /// Changing the provider clears <see cref="IOptionsMonitorCache{TOptions}"/> on every node and
    /// nothing else, so the policy has to follow that cache rather than hold a copy — this is the
    /// no-restart guarantee of the settings panel, seen from the header.
    /// </summary>
    [Fact]
    public void Should_follow_the_provider_when_it_changes_without_a_restart()
    {
        var options = Substitute.For<IOptionsMonitor<OpenIdConnectOptions>>();
        options.Get(DynamicExternalIdProviderOptions.OptionsName)
            .Returns(Configured("https://first.example.com"), Configured("https://second.example.com"));

        var sut = new ExternalLoginFormActionOrigin(options);

        sut.Resolve(Request()).Should().Be("https://first.example.com");
        sut.Resolve(Request()).Should().Be("https://second.example.com");
    }

    private static OpenIdConnectOptions Configured(string authority)
        => new() { Authority = authority, ClientId = "suite" };

    private static string? Resolve(OpenIdConnectOptions provider)
    {
        var options = Substitute.For<IOptionsMonitor<OpenIdConnectOptions>>();
        options.Get(DynamicExternalIdProviderOptions.OptionsName).Returns(provider);

        return new ExternalLoginFormActionOrigin(options).Resolve(Request());
    }

    private static HttpContext Request()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("suite.example.com");

        return context;
    }
}
