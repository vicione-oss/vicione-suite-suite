using System.Globalization;
using System.IO.Abstractions.TestingHelpers;
using AwesomeAssertions;
using Core.OS.Hosting;
using Core.OS.Hosting.Extensions;
using Core.OS.Instance;
using Microsoft.AspNetCore.Builder;
using NSubstitute;
using Sdk.Instance;
using Xunit;

namespace Core.OS.Tests.Hosting.Extensions;

public class WebApplicationBuilderExtensionsTests
{
    private readonly MockFileSystem _fileSystem = new();
    private readonly Serilog.ILogger _logger = Substitute.For<Serilog.ILogger>();
    private readonly InstanceOptions _instanceOptions;

    public WebApplicationBuilderExtensionsTests()
    {
        _instanceOptions = new InstanceOptions
        {
            HomeDirectory = _fileSystem.Path.GetFullPath("AppData"),
            CacheDirectory = _fileSystem.Path.GetFullPath("Cache"),
            BackupDirectory = _fileSystem.Path.GetFullPath("Backup"),
            Type = InstanceType.Standalone,
        };

        _fileSystem.AddDirectory(_instanceOptions.CacheDirectory);
        _fileSystem.AddDirectory(_instanceOptions.HomeDirectory);
    }

    public class DetectVersionDowngrade : WebApplicationBuilderExtensionsTests
    {
        [Fact]
        public async Task Should_ensure_data_version_gets_persisted()
        {
            // Arrange
            var builder = WebApplication.CreateBuilder();

            // Act
            var detected = await builder.DetectVersionDowngrade(_fileSystem, _instanceOptions, _logger, TestContext.Current.CancellationToken);

            // Assert
            detected.Should().BeFalse();

            var dataVersionFilePath = _fileSystem.GetLocalDataVersionFilePath(_instanceOptions);
            _fileSystem.File.Exists(dataVersionFilePath).Should().BeTrue();
            (await _fileSystem.File.ReadAllTextAsync(dataVersionFilePath, TestContext.Current.CancellationToken)).Should().Be(SuiteVersionUtils.GetSuiteVersion());
        }

        [Fact]
        public async Task Should_return_false_for_matching_versions()
        {
            // Arrange
            var builder = WebApplication.CreateBuilder();
            var version = GetSuiteVersionIncrement();
            await _fileSystem.WriteDataVersionFile(_instanceOptions, version, TestContext.Current.CancellationToken);

            // Act
            var detected = await builder.DetectVersionDowngrade(_fileSystem, _instanceOptions, _logger, TestContext.Current.CancellationToken);

            // Assert
            detected.Should().BeFalse();
            _fileSystem.File.Exists(_fileSystem.GetLocalDataVersionFilePath(_instanceOptions)).Should().BeTrue();
        }

        [Fact]
        public async Task Should_return_false_for_older_versions()
        {
            // Arrange
            var builder = WebApplication.CreateBuilder();
            var version = GetSuiteVersionIncrement(incrementMinor: -1);
            await _fileSystem.WriteDataVersionFile(_instanceOptions, version, TestContext.Current.CancellationToken);

            // Act
            var detected = await builder.DetectVersionDowngrade(_fileSystem, _instanceOptions, _logger, TestContext.Current.CancellationToken);

            // Assert
            detected.Should().BeFalse();
            _fileSystem.File.Exists(_fileSystem.GetLocalDataVersionFilePath(_instanceOptions)).Should().BeTrue();
        }

        [Fact]
        public async Task Should_return_true_if_dataversion_is_lower_than_suite_version()
        {
            // Arrange
            var builder = WebApplication.CreateBuilder();
            var version = GetSuiteVersionIncrement(incrementMinor: -1);
            await _fileSystem.WriteDataVersionFile(_instanceOptions, version, TestContext.Current.CancellationToken);

            // Act
            var detected = await builder.DetectVersionDowngrade(_fileSystem, _instanceOptions, _logger, TestContext.Current.CancellationToken);

            // Assert
            detected.Should().BeFalse();
            var dataVersionFilePath = _fileSystem.GetLocalDataVersionFilePath(_instanceOptions);
            _fileSystem.File.Exists(dataVersionFilePath).Should().BeTrue();
            (await _fileSystem.File.ReadAllTextAsync(dataVersionFilePath, TestContext.Current.CancellationToken)).Should().Be(SuiteVersionUtils.GetSuiteVersion());
        }

        [Fact]
        public async Task Should_return_false_on_empty_dataversion_and_log_error()
        {
            // Arrange
            var builder = WebApplication.CreateBuilder();
            await _fileSystem.WriteDataVersionFile(_instanceOptions, string.Empty, TestContext.Current.CancellationToken);

            // Act
            await builder.DetectVersionDowngrade(_fileSystem, _instanceOptions, _logger, TestContext.Current.CancellationToken);

            // Assert
            var dataVersionFilePath = _fileSystem.GetLocalDataVersionFilePath(_instanceOptions);
            _fileSystem.File.Exists(dataVersionFilePath).Should().BeTrue();
            (await _fileSystem.File.ReadAllTextAsync(dataVersionFilePath, TestContext.Current.CancellationToken)).Should().Be(SuiteVersionUtils.GetSuiteVersion());

            _logger.Received().Error(Arg.Any<InvalidOperationException>(), "Error on detecting downgrade");
        }


        [Fact]
        public async Task Should_run_minimal_host_on_downgrade_detected()
        {
            // Arrange
            var builder = WebApplication.CreateBuilder();
            var version = GetSuiteVersionIncrement(incrementMinor: 1);
            await _fileSystem.WriteDataVersionFile(_instanceOptions, version, TestContext.Current.CancellationToken);
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(1));

            // Act
            var detected = await builder.DetectVersionDowngrade(_fileSystem, _instanceOptions, _logger, cancel.Token);

            // Assert
            detected.Should().BeTrue();
            _fileSystem.File.Exists(_fileSystem.GetLocalDataVersionFilePath(_instanceOptions)).Should().BeTrue();
        }

        private static string GetSuiteVersionIncrement(int incrementMajor = 0, int incrementMinor = 0, int incrementPatch = 0)
        {
            var version = SuiteVersionUtils.GetSuiteVersion();
            var parts = version.Split('.');
            if (parts.Length != 3)
                throw new InvalidOperationException($"Invalid format of version:{version}");

            var major = int.Parse(parts[0], CultureInfo.InvariantCulture) + incrementMajor;
            var minor = int.Parse(parts[1], CultureInfo.InvariantCulture) + incrementMinor;
            var patch = int.Parse(parts[2], CultureInfo.InvariantCulture) + incrementPatch;

            return $"{major}.{minor}.{patch}";
        }
    }
}
