using System.Globalization;
using System.IO.Abstractions.TestingHelpers;
using AwesomeAssertions;
using Core.OS.Hosting;
using Core.OS.Hosting.Contracts;
using Core.OS.Hosting.Extensions;
using Core.OS.Instance;
using NSubstitute;
using Sdk.Instance;
using Xunit;

namespace Core.OS.Tests.Hosting.Extensions;

public class IFileSystemExtensionsTests
{
    private readonly MockFileSystem _fileSystem = new();
    private readonly Serilog.ILogger _logger = Substitute.For<Serilog.ILogger>();
    private readonly InstanceOptions _instanceOptions;

    public IFileSystemExtensionsTests()
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

    public class DetectVersionDowngrade : IFileSystemExtensionsTests
    {
        [Fact]
        public async Task Should_ensure_data_version_gets_persisted()
        {
            // Arrange / Act
            var result = await _fileSystem.DetectVersionDowngrade(_instanceOptions, _logger, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeNull();

            var dataVersionFilePath = _fileSystem.GetLocalDataVersionFilePath(_instanceOptions);
            _fileSystem.File.Exists(dataVersionFilePath).Should().BeTrue();
            (await _fileSystem.File.ReadAllTextAsync(dataVersionFilePath, TestContext.Current.CancellationToken)).Should().Be(SuiteVersionUtils.GetSuiteVersion());
        }

        [Fact]
        public async Task Should_return_null_for_matching_versions()
        {
            // Arrange
            var version = GetSuiteVersionIncrement();
            await _fileSystem.WriteDataVersionFile(_instanceOptions, version, TestContext.Current.CancellationToken);

            // Act
            var result = await _fileSystem.DetectVersionDowngrade(_instanceOptions, _logger, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeNull();
            _fileSystem.File.Exists(_fileSystem.GetLocalDataVersionFilePath(_instanceOptions)).Should().BeTrue();
        }

        [Fact]
        public async Task Should_return_null_for_suite_version_higher_than_persisted()
        {
            // Arrange
            var version = GetSuiteVersionIncrement(incrementMinor: -1);
            await _fileSystem.WriteDataVersionFile(_instanceOptions, version, TestContext.Current.CancellationToken);

            // Act
            var result = await _fileSystem.DetectVersionDowngrade(_instanceOptions, _logger, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeNull();
            var dataVersionFilePath = _fileSystem.GetLocalDataVersionFilePath(_instanceOptions);
            _fileSystem.File.Exists(dataVersionFilePath).Should().BeTrue();
            (await _fileSystem.File.ReadAllTextAsync(dataVersionFilePath, TestContext.Current.CancellationToken)).Should().Be(SuiteVersionUtils.GetSuiteVersion());
        }

        [Fact]
        public async Task Should_return_null_for_patch_downgrade()
        {
            // Arrange
            var version = GetSuiteVersionIncrement(incrementPatch: 1);
            await _fileSystem.WriteDataVersionFile(_instanceOptions, version, TestContext.Current.CancellationToken);

            // Act
            var result = await _fileSystem.DetectVersionDowngrade(_instanceOptions, _logger, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeNull();
            var dataVersionFilePath = _fileSystem.GetLocalDataVersionFilePath(_instanceOptions);
            _fileSystem.File.Exists(dataVersionFilePath).Should().BeTrue();
            (await _fileSystem.File.ReadAllTextAsync(dataVersionFilePath, TestContext.Current.CancellationToken)).Should().Be(SuiteVersionUtils.GetSuiteVersion());
        }

        [Fact]
        public async Task Should_throw_on_empty_data_version_and_restore_current_version()
        {
            // Arrange
            await _fileSystem.WriteDataVersionFile(_instanceOptions, string.Empty, TestContext.Current.CancellationToken);

            // Act
            var act = () => _fileSystem.DetectVersionDowngrade(_instanceOptions, _logger, TestContext.Current.CancellationToken);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();

            var dataVersionFilePath = _fileSystem.GetLocalDataVersionFilePath(_instanceOptions);
            (await _fileSystem.File.ReadAllTextAsync(dataVersionFilePath, TestContext.Current.CancellationToken)).Should().Be(SuiteVersionUtils.GetSuiteVersion());
        }

        [Fact]
        public async Task Should_return_downgrade_information_when_persisted_version_is_higher()
        {
            // Arrange
            var version = GetSuiteVersionIncrement(incrementMinor: 1);
            await _fileSystem.WriteDataVersionFile(_instanceOptions, version, TestContext.Current.CancellationToken);

            // Act
            var result = await _fileSystem.DetectVersionDowngrade(_instanceOptions, _logger, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<VersionDowngradeInformation>();
            result!.CurrentVersion.Should().Be(SuiteVersionUtils.GetSuiteVersion());
            result.DataVersion.Should().Be(version);
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
