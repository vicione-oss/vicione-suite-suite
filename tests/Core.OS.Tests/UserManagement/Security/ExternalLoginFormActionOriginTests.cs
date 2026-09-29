using Core.OS.UserManagement.Configuration;
using Core.OS.UserManagement.Security;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Core.OS.Tests.UserManagement.Security;

public class ExternalLoginFormActionOriginTests
{
    [Theory]
    [InlineData("https://idp.example.com", "https://idp.example.com")]
    [InlineData("https://idp.example.com/realms/suite", "https://idp.example.com")]
    [InlineData("https://idp.example.com:8443/realms/suite", "https://idp.example.com:8443")]
    public void Should_name_the_origin_of_the_configured_authority(string authority, string expected)
    {
        // Arrange
        var provider = Configured(authority);

        // Act
        var origin = Resolve(provider);

        // Assert
        origin.Should().Be(expected);
    }

    /// <summary>
    /// Kestrel fails a response whose headers are not ASCII, so a Unicode host would break every page.
    /// </summary>
    [Fact]
    public void Should_name_an_internationalised_host_in_its_ascii_form()
    {
        // Arrange
        var provider = Configured("https://bücher.example/realms/suite");

        // Act
        var origin = Resolve(provider);

        // Assert
        origin.Should().Be("https://xn--bcher-kva.example");
    }

    [Fact]
    public void Should_name_no_origin_while_no_provider_is_configured()
    {
        // Arrange
        var unconfigured = new OpenIdConnectOptions
        {
            Authority = "https://unconfigured.local",
            ClientId = Constants.UnconfiguredClient
        };

        // Act
        var origin = Resolve(unconfigured);

        // Assert
        origin.Should().BeNull();
    }

    [Fact]
    public void Should_name_no_origin_when_the_authority_is_the_suite_itself()
    {
        // Arrange
        var provider = Configured("https://suite.example.com/oidc");

        // Act
        var origin = Resolve(provider);

        // Assert
        origin.Should().BeNull();
    }

    [Fact]
    public void Should_name_no_origin_when_the_authority_is_not_a_usable_url()
    {
        // Arrange
        var provider = Configured("not-a-url");

        // Act
        var origin = Resolve(provider);

        // Assert
        origin.Should().BeNull();
    }

    [Fact]
    public void Should_follow_the_provider_when_it_changes_without_a_restart()
    {
        // Arrange
        var options = Substitute.For<IOptionsMonitor<OpenIdConnectOptions>>();
        options.Get(DynamicExternalIdProviderOptions.OptionsName)
            .Returns(Configured("https://first.example.com"), Configured("https://second.example.com"));
        var sut = new ExternalLoginFormActionOrigin(options);

        // Act
        var first = sut.Resolve(Request());
        var second = sut.Resolve(Request());

        // Assert
        first.Should().Be("https://first.example.com");
        second.Should().Be("https://second.example.com");
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
