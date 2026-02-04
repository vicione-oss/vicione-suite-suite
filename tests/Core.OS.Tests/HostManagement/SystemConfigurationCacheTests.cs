using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
using Core.OS.HostManagement;
using Core.Shared.HostManagement;
using HostManagement.Shared.Contracts;
using AwesomeAssertions;

namespace Core.OS.Tests.HostManagement;

public class SystemConfigurationCacheTests
{
    private readonly ILogger<SystemConfigurationCache> _logger = Substitute.For<ILogger<SystemConfigurationCache>>();
    private readonly HostManagementOptions _options = new()
    {
        ConfigurationCacheLifetimeMs = 200
    };

    public ServiceProvider SetupServiceProvider()
        => new ServiceCollection()
            .AddSingleton(_logger)
            .AddSingleton(Options.Create(_options))
            .AddSingleton<SystemConfigurationCache>()
            .BuildServiceProvider();

    public class Set : SystemConfigurationCacheTests
    {
        [Fact]
        public void Should_set_cache_system_config()
        {
            // Arrange
            var config = TestPipeClient.GetEmbeddedSystemConfiguration();
            using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<SystemConfigurationCache>();

            // Act
            cache.Set(config);

            // Assert
            cache.Get().Should().Be(config);
        }
    }

    public class Get : SystemConfigurationCacheTests
    {
        [Fact]
        public void Should_get_cached_system_config()
        {
            // Arrange
            var config = TestPipeClient.GetEmbeddedSystemConfiguration();
            using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<SystemConfigurationCache>();
            cache.Set(config);

            // Act
            var cached = cache.Get();

            // Assert
            cached.Should().Be(config);
        }

        [Fact]
        public async Task Should_return_null_if_cache_lifetime_exceeded()
        {
            // Arrange
            var config = new SystemConfiguration();
            using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<SystemConfigurationCache>();
            cache.Set(config);

            // Act
            await Task.Delay((int)_options.ConfigurationCacheLifetimeMs + 500);
            var cached = cache.Get();

            // Assert
            cached.Should().BeNull();
        }
    }

    public class Invalidate : SystemConfigurationCacheTests
    {
        [Fact]
        public void Should_invalidate_cache()
        {
            // Arrange
            var config = new SystemConfiguration();
            using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<SystemConfigurationCache>();
            cache.Set(config);

            // Act
            cache.Invalidate();

            // Assert
            cache.Get().Should().BeNull();
        }

        [Fact]
        public void Should_be_callable_multiple_times()
        {
            // Arrange
            var config = new SystemConfiguration();
            using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<SystemConfigurationCache>();

            // Act
            for (var i = 0; i < 10; i++)
            {
                cache.Set(config);
                cache.Invalidate();
                cache.Invalidate();
                cache.Invalidate();
            }

            // Assert
            cache.Get().Should().BeNull();
        }
    }
}
