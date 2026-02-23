using AwesomeAssertions;
using Core.OS.UserManagement.Security;
using Core.Shared.UserManagement.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Core.OS.Tests.UserManagement.Security;

public class ExternalAuthenticationSettingsTest
{
    [Fact]
    public async Task Should_return_false_if_no_providers_are_configured()
    {
        // Arrange
        var options = new ExternalIdProviderOptions { Providers = new List<ExternalIdProvider>() };
        var sut = CreateSut(options);

        // Act
        var result = await sut.IsExternalAuthenticationProviderConfigured();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task Should_return_true_if_single_provider_is_configured()
    {
        // Arrange
        var options = new ExternalIdProviderOptions
        {
            Providers = new List<ExternalIdProvider>
            {
                new()
                {
                    Name = "Provider1",
                    Authority = "https://authority.example.com",
                    ClientId = "ClientId1",
                    ClientSecret = "ClientSecret1"
                }
            }
        };
        var sut = CreateSut(options);

        // Act
        var result = await sut.IsExternalAuthenticationProviderConfigured();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task Should_return_true_even_if_multiple_providers_are_configured()
    {
        // Arrange
        var options = new ExternalIdProviderOptions
        {
            Providers = new List<ExternalIdProvider>
            {
                new()
                {
                    Name = "ProviderThatShouldBeFound",
                    Authority = "https://authority.example.com",
                    ClientId = "ClientId1",
                    ClientSecret = "ClientSecret1"
                },
                new()
                {
                    Name = "IShallNotBeFound",
                    Authority = "https://respect.my.authority.com",
                    ClientId = "ClientId2",
                    ClientSecret = "ClientSecret2"
                }
            }
        };
        var sut = CreateSut(options);

        // Act
        // Assert
        (await sut.IsExternalAuthenticationProviderConfigured()).Should().BeTrue();
    }


    private static ExternalAuthenticationSettings CreateSut(ExternalIdProviderOptions options)
    {
        var optionsWrapper = new OptionsWrapper<ExternalIdProviderOptions>(options);
        return new ExternalAuthenticationSettings(optionsWrapper,
            Substitute.For<ILogger<ExternalAuthenticationSettings>>());
    }
}
