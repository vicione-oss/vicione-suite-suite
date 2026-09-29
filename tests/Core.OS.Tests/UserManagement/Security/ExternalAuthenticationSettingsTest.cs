using Core.OS.DbContext;
using Core.OS.UserManagement.Configuration;
using Core.OS.UserManagement.Security;
using Core.Shared.UserManagement.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.UserManagement.Security;

public sealed class ExternalAuthenticationSettingsTest : IAsyncDisposable
{
    private readonly RecordingLogger logger = new();
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
        result.Should().BeFalse();
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
        result.Should().BeFalse();
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
        result.Should().BeTrue();
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
        result.Should().BeTrue();
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

    [Fact]
    public async Task Should_prefer_the_database_provider_over_the_one_in_config()
    {
        // Arrange
        // Config-first, the sentinel would hide the login button while sign-in uses the stored provider.
        var options = new ExternalIdProviderOptions
        {
            Providers =
            [
                new ExternalIdProvider
                {
                    Name = "ConfigProvider",
                    Authority = "https://config.example.com",
                    ClientId = Constants.UnconfiguredClient,
                    ClientSecret = null
                }
            ]
        };
        var sut = CreateSut(options,
            context =>
            {
                context.ExternalIdProviders.Add(new Shared.UserManagement.Contracts.ExternalIdProvider
                {
                    Authority = "https://db.example.com",
                    ClientId = "DbClientId",
                    ClientSecret = "DbSecret",
                    Name = "DbProvider"
                });
                context.SaveChanges();
            });

        // Act
        var result = await sut.IsExternalAuthenticationProviderConfigured();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Should_not_warn_when_the_database_provider_takes_precedence_over_the_one_in_config()
    {
        // Arrange
        var options = new ExternalIdProviderOptions
        {
            Providers =
            [
                new ExternalIdProvider
                {
                    Name = "ConfigProvider",
                    Authority = "https://config.example.com",
                    ClientId = "ConfigClientId",
                    ClientSecret = null
                }
            ]
        };
        var sut = CreateSut(options,
            context =>
            {
                context.ExternalIdProviders.Add(new Shared.UserManagement.Contracts.ExternalIdProvider
                {
                    Authority = "https://db.example.com",
                    ClientId = "DbClientId",
                    ClientSecret = "DbSecret",
                    Name = "DbProvider"
                });
                context.SaveChanges();
            });

        // Act
        await sut.IsExternalAuthenticationProviderConfigured();

        // Assert
        logger.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_warn_when_the_config_holds_more_than_one_provider()
    {
        // Arrange
        var options = new ExternalIdProviderOptions
        {
            Providers =
            [
                new ExternalIdProvider
                {
                    Name = "Provider1",
                    Authority = "https://first.example.com",
                    ClientId = "ClientId1",
                    ClientSecret = null
                },
                new ExternalIdProvider
                {
                    Name = "Provider2",
                    Authority = "https://second.example.com",
                    ClientId = "ClientId2",
                    ClientSecret = null
                }
            ]
        };
        var sut = CreateSut(options);

        // Act
        await sut.IsExternalAuthenticationProviderConfigured();

        // Assert
        logger.Warnings.Should().ContainSingle();
    }

    private ExternalAuthenticationSettings CreateSut(ExternalIdProviderOptions options,
        Action<ApplicationDbContext>? configureDb = null)
    {
        applicationDbContext = TestDbContextFactory.CreateSqliteContext<ApplicationDbContextSqlite>();
        configureDb?.Invoke(applicationDbContext);

        var optionsWrapper = new OptionsWrapper<ExternalIdProviderOptions>(options);
        return new ExternalAuthenticationSettings(optionsWrapper,
            applicationDbContext,
            logger);
    }

    public async ValueTask DisposeAsync()
    {
        if (applicationDbContext != null)
            await applicationDbContext.DisposeAsync();
    }

    private sealed class RecordingLogger : ILogger<ExternalAuthenticationSettings>
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
                Warnings.Add(formatter(state, exception));
        }
    }
}
