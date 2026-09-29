using Core.OS.DbContext;
using Core.OS.UserManagement.Configuration;
using Core.Shared.Instance.Contracts;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace Core.OS.Tests.UserManagement.Configuration;

public sealed class ExternalIdProviderReplicationObserverTests
{
    private static readonly string ApplicationContextType = typeof(IApplicationDbContext).FullName!;

    private readonly IOptionsMonitorCache<OpenIdConnectOptions> _optionsCache = new OptionsCache<OpenIdConnectOptions>();
    private readonly OpenIdConnectOptions _cachedOptions = new() { Authority = "https://stale.example.com" };

    [Fact]
    public void Should_drop_the_cached_options_when_replication_writes_the_provider()
    {
        // Arrange
        _optionsCache.TryAdd(DynamicExternalIdProviderOptions.OptionsName, _cachedOptions);
        var sut = new ExternalIdProviderReplicationObserver(_optionsCache);

        // Act
        sut.ChangeSetApplied(ApplicationContextType, new HashSet<string> { typeof(ExternalIdProvider).FullName! });

        // Assert
        _optionsCache.TryAdd(DynamicExternalIdProviderOptions.OptionsName, _cachedOptions)
            .Should().BeTrue("the entry was removed, so it can be added again");
    }

    [Fact]
    public void Should_keep_the_cached_options_when_replication_writes_other_entities()
    {
        // Arrange
        _optionsCache.TryAdd(DynamicExternalIdProviderOptions.OptionsName, _cachedOptions);
        var sut = new ExternalIdProviderReplicationObserver(_optionsCache);

        // Act
        sut.ChangeSetApplied(ApplicationContextType, new HashSet<string> { typeof(InstanceInformation).FullName! });

        // Assert
        _optionsCache.TryAdd(DynamicExternalIdProviderOptions.OptionsName, _cachedOptions)
            .Should().BeFalse("the entry is still cached");
    }

    [Fact]
    public void Should_drop_the_cached_options_after_a_full_sync()
    {
        // Arrange
        _optionsCache.TryAdd(DynamicExternalIdProviderOptions.OptionsName, _cachedOptions);
        var sut = new ExternalIdProviderReplicationObserver(_optionsCache);

        // Act
        sut.Resynchronized();

        // Assert
        _optionsCache.TryAdd(DynamicExternalIdProviderOptions.OptionsName, _cachedOptions)
            .Should().BeTrue("the entry was removed, so it can be added again");
    }
}
