using AwesomeAssertions;
using Core.OS.DbContext;
using Core.OS.UserManagement.Configuration;
using Core.OS.UserManagement.Security;
using Core.Shared.UserManagement.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.UserManagement.Security;

public sealed class ExternalAuthenticationSettingsTest : IAsyncDisposable
{
    private ApplicationDbContextSqlite? applicationDbContext;

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
    public async Task Should_return_false_if_only_the_sentinel_provider_is_found()
    {
        // Arrange
        var options = new ExternalIdProviderOptions
        {
            Providers =
            [
                new ExternalIdProvider
                {
                    ClientId = Constants.UnconfiguredClient,
                    Authority = "https://authority.example.com",
                    Name = "Provider1",
                    ClientSecret = "ClientSecret1"
                }
            ]
        };
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
    public async Task Should_return_true_if_db_providers_are_configured()
    {
        // Arrange
        var options = new ExternalIdProviderOptions { Providers = new List<ExternalIdProvider>() };
        var sut = CreateSut(options,
            context =>
            {
                context.ExternalIdProviders.Add(new Shared.UserManagement.Contracts.ExternalIdProvider
                {
                    Authority = "https://authority.example.com",
                    ClientId = "ClientId1",
                    ClientSecret = "ClientSecret1",
                    Name = "Provider1"
                });
                context.SaveChanges();
            });

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
        var sut = CreateSut(options,
            context =>
            {
                context.ExternalIdProviders.Add(new Shared.UserManagement.Contracts.ExternalIdProvider
                {
                    Authority = "https://authority.example.com",
                    ClientId = "ClientId1",
                    ClientSecret = "ClientSecret1",
                    Name = "Provider1"
                });
                context.SaveChanges();
            });

        // Act
        // Assert
        (await sut.IsExternalAuthenticationProviderConfigured()).Should().BeTrue();
    }

    private ExternalAuthenticationSettings CreateSut(ExternalIdProviderOptions options,
        Action<ApplicationDbContext>? configureDb = null)
    {
        applicationDbContext = TestDbContextFactory.CreateSqliteContext<ApplicationDbContextSqlite>();
        configureDb?.Invoke(applicationDbContext);

        var optionsWrapper = new OptionsWrapper<ExternalIdProviderOptions>(options);
        return new ExternalAuthenticationSettings(optionsWrapper,
            applicationDbContext,
            Substitute.For<ILogger<ExternalAuthenticationSettings>>());
    }

    public async ValueTask DisposeAsync()
    {
        if (applicationDbContext != null)
            await applicationDbContext.DisposeAsync();
    }
}
