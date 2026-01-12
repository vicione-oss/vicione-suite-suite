using AwesomeAssertions;
using Core.OS.Connections.Mqtt;
using Core.OS.Instance;
using Core.OS.Monitoring.Extensions;
using Core.Shared.Monitoring;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Journal;
using ViciOne.Journal;
using ViciOne.SystemMonitoring.Configuration;
using Xunit;

namespace Core.OS.Tests.Monitoring.Extensions;

public sealed class IServiceCollectionExtensionsTests
{
    public sealed class AddJournalService
    {
        [Fact]
        public void Should_register_journal_services_when_on_linux()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddJournalService();

            // Assert
            if (!OperatingSystem.IsLinux())
                return;

            services.Should().Contain(s => s.ServiceType == typeof(JournalService));

            using var sp = services.BuildServiceProvider();
            sp.GetService<IOptions<JournalOptions>>().Should().NotBeNull();
        }

        [Fact]
        public void Should_not_register_anything_when_not_linux()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddJournalService();

            // Assert
            if (OperatingSystem.IsLinux())
                return;

            services.Should().NotContain(s => s.ServiceType == typeof(JournalService));
            services.Should().NotContain(s => s.ServiceType == typeof(IOptions<JournalOptions>));
        }
    }

    public sealed class AddSystemMonitoring
    {
        [Fact]
        public void Should_register_journal_monitoring_and_configure_options()
        {
            // Arrange
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "SystemMonitoring:Enabled", "true" },
                    { "SystemMonitoring:SuiteJournalFilter", "f" },
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(config);
            services.AddSingleton<IOptions<MqttClientOptions>>(Options.Create(new MqttClientOptions()));
            services.AddSingleton<ILocalInstanceInformationProvider>(Substitute.For<ILocalInstanceInformationProvider>());

            // Act
            services.AddSystemMonitoring(config);

            // Assert
            if (!OperatingSystem.IsLinux())
                return;

            services.Should().Contain(s => s.ServiceType == typeof(IJournalMonitoring));

            using var sp = services.BuildServiceProvider();
            sp.GetService<IOptions<SystemMonitoringOptions>>().Should().NotBeNull();
            sp.GetService<IOptions<MonitoringConfig>>().Should().NotBeNull();
        }

        [Fact]
        public void Should_not_register_monitoringconfig_when_not_linux()
        {
            // Arrange
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "SystemMonitoring:Enabled", "true" },
                    { "SystemMonitoring:SuiteJournalFilter", "f" },
                })
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);

            // Act
            services.AddSystemMonitoring(config);

            // Assert
            if (OperatingSystem.IsLinux())
                return;

            using var sp = services.BuildServiceProvider();
            sp.GetService<IOptions<MonitoringConfig>>().Should().NotBeNull();
        }
    }
}
