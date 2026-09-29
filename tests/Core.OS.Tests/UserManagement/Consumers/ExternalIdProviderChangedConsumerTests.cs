using Core.OS.UserManagement.Configuration;
using Core.OS.UserManagement.Consumers;
using Core.Shared.UserManagement.Events;
using MassTransit;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.UserManagement.Consumers;

public sealed class ExternalIdProviderChangedConsumerTests
{
    private readonly IOptionsMonitorCache<OpenIdConnectOptions> _optionsCache
        = new OptionsCache<OpenIdConnectOptions>();

    private MassTransitTester CreateTester()
        => new(cfg =>
        {
            cfg.AddConsumer<ExternalIdProviderChangedConsumer>();
            cfg.AddSingleton(_optionsCache);
        });

    [Fact]
    public async Task Should_drop_the_cached_options_for_the_openid_connect_scheme()
    {
        // Arrange
        var cachedOptions = new OpenIdConnectOptions { Authority = "https://stale.example.com" };
        _optionsCache.TryAdd(DynamicExternalIdProviderOptions.OptionsName, cachedOptions);

        await using var tester = CreateTester();

        // Act
        await tester.Harness.TestEvent<ExternalIdProviderChanged, ExternalIdProviderChangedConsumer>(
            new ExternalIdProviderChanged(Guid.NewGuid()));

        // Assert
        _optionsCache.TryAdd(DynamicExternalIdProviderOptions.OptionsName, cachedOptions)
            .Should().BeTrue("the entry was removed, so it can be added again");
    }

    [Fact]
    public async Task Should_leave_other_schemes_cached()
    {
        // Arrange
        var otherSchemeOptions = new OpenIdConnectOptions { Authority = "https://other.example.com" };
        _optionsCache.TryAdd("SomeOtherScheme", otherSchemeOptions);

        await using var tester = CreateTester();

        // Act
        await tester.Harness.TestEvent<ExternalIdProviderChanged, ExternalIdProviderChangedConsumer>(
            new ExternalIdProviderChanged(Guid.NewGuid()));

        // Assert
        _optionsCache.TryAdd("SomeOtherScheme", otherSchemeOptions)
            .Should().BeFalse("the entry is still cached");
    }
}
