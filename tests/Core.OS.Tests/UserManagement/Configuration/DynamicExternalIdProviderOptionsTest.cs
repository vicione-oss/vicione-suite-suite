using Core.OS.DbContext;
using Core.OS.UserManagement.Configuration;
using Core.Shared.UserManagement.Configuration;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.UserManagement.Configuration;

public class DynamicExternalIdProviderOptionsTest
{
    [Fact]
    public void Should_not_change_the_options_when_the_name_is_not_the_expected_default()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.CreateSqliteContext<ApplicationDbContextSqlite>();
        dbContext.ExternalIdProviders.Add(new Shared.UserManagement.Contracts.ExternalIdProvider
        {
            Authority = "https://db.example.com",
            ClientId = "DbClientId",
            ClientSecret = "DbSecret",
            Name = "DbProvider"
        });
        dbContext.SaveChanges();

        var configuration = CreateConfiguration(new ExternalIdProviderOptions { Providers = [] });
        var serviceProvider = CreateServiceProvider(dbContext);
        var sut = new DynamicExternalIdProviderOptions(serviceProvider, configuration);
        var options = new OpenIdConnectOptions();
        var originalAuthority = options.Authority;
        var originalClientId = options.ClientId;

        // Act
        sut.Configure("SomeOtherName", options);

        // Assert
        Assert.Equal(originalAuthority, options.Authority);
        Assert.Equal(originalClientId, options.ClientId);
    }

    [Fact]
    public void Should_mark_the_provider_as_unconfigured_when_there_is_none_in_config_and_database()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.CreateSqliteContext<ApplicationDbContextSqlite>();
        var configuration = CreateConfiguration(new ExternalIdProviderOptions { Providers = [] });
        var serviceProvider = CreateServiceProvider(dbContext);
        var sut = new DynamicExternalIdProviderOptions(serviceProvider, configuration);
        var options = new OpenIdConnectOptions();

        // Act
        sut.Configure(DynamicExternalIdProviderOptions.OptionsName, options);

        // Assert
        Assert.Equal(Constants.UnconfiguredClient, options.ClientId);
        Assert.Equal("https://unconfigured.local", options.Authority);
    }

    [Fact]
    public void Should_pick_the_first_provider_in_database_when_there_are_multiple_providers()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.CreateSqliteContext<ApplicationDbContextSqlite>();
        dbContext.ExternalIdProviders.Add(new Shared.UserManagement.Contracts.ExternalIdProvider
        {
            Authority = "https://first.example.com",
            ClientId = "FirstClientId",
            ClientSecret = "FirstSecret",
            Name = "FirstProvider"
        });
        dbContext.ExternalIdProviders.Add(new Shared.UserManagement.Contracts.ExternalIdProvider
        {
            Authority = "https://second.example.com",
            ClientId = "SecondClientId",
            ClientSecret = "SecondSecret",
            Name = "SecondProvider"
        });
        dbContext.SaveChanges();
        var firstProviderInDb = dbContext.ExternalIdProviders.First();

        var configuration = CreateConfiguration(new ExternalIdProviderOptions { Providers = [] });
        var serviceProvider = CreateServiceProvider(dbContext);
        var sut = new DynamicExternalIdProviderOptions(serviceProvider, configuration);
        var options = new OpenIdConnectOptions();

        // Act
        sut.Configure(DynamicExternalIdProviderOptions.OptionsName, options);

        // Assert
        Assert.Equal(firstProviderInDb.ClientId, options.ClientId);
        Assert.Equal(firstProviderInDb.Authority, options.Authority);
    }

    [Fact]
    public void Should_pick_the_provider_from_config_if_it_is_the_only_configured_provider()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.CreateSqliteContext<ApplicationDbContextSqlite>();
        var configuration = CreateConfiguration(new ExternalIdProviderOptions
        {
            Providers =
            [
                new ExternalIdProvider
                {
                    Name = "ConfigProvider",
                    Authority = "https://config.example.com",
                    ClientId = "ConfigClientId",
                    ClientSecret = "ConfigSecret"
                }
            ]
        });
        var serviceProvider = CreateServiceProvider(dbContext);
        var sut = new DynamicExternalIdProviderOptions(serviceProvider, configuration);
        var options = new OpenIdConnectOptions();

        // Act
        sut.Configure(DynamicExternalIdProviderOptions.OptionsName, options);

        // Assert
        Assert.Equal("ConfigClientId", options.ClientId);
        Assert.Equal("https://config.example.com", options.Authority);
    }

    [Fact]
    public void Should_pick_the_one_in_the_database_over_the_one_in_config()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.CreateSqliteContext<ApplicationDbContextSqlite>();
        dbContext.ExternalIdProviders.Add(new Shared.UserManagement.Contracts.ExternalIdProvider
        {
            Authority = "https://db.example.com",
            ClientId = "DbClientId",
            ClientSecret = "DbSecret",
            Name = "DbProvider"
        });
        dbContext.SaveChanges();

        var configuration = CreateConfiguration(new ExternalIdProviderOptions
        {
            Providers =
            [
                new ExternalIdProvider
                {
                    Name = "ConfigProvider",
                    Authority = "https://config.example.com",
                    ClientId = "ConfigClientId",
                    ClientSecret = "ConfigSecret"
                }
            ]
        });
        var serviceProvider = CreateServiceProvider(dbContext);
        var sut = new DynamicExternalIdProviderOptions(serviceProvider, configuration);
        var options = new OpenIdConnectOptions();

        // Act
        sut.Configure(DynamicExternalIdProviderOptions.OptionsName, options);

        // Assert
        Assert.Equal("DbClientId", options.ClientId);
        Assert.Equal("https://db.example.com", options.Authority);
    }

    private static IConfiguration CreateConfiguration(ExternalIdProviderOptions options)
    {
        var configValues = new Dictionary<string, string?>();

        for (var i = 0; i < options.Providers.Count; i++)
        {
            var provider = options.Providers[i];
            configValues[$"{ExternalIdProviderOptions.ConfigSection}:Providers:{i}:Name"] = provider.Name;
            configValues[$"{ExternalIdProviderOptions.ConfigSection}:Providers:{i}:Authority"] = provider.Authority;
            configValues[$"{ExternalIdProviderOptions.ConfigSection}:Providers:{i}:ClientId"] = provider.ClientId;
            configValues[$"{ExternalIdProviderOptions.ConfigSection}:Providers:{i}:ClientSecret"]
                = provider.ClientSecret;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
    }

    private static IServiceProvider CreateServiceProvider(ApplicationDbContext dbContext)
    {
        var services = new ServiceCollection();
        services.AddSingleton(dbContext);
        services.AddTransient<ApplicationDbContext>(_ => dbContext);
        return services.BuildServiceProvider();
    }
}
