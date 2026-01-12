using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using Blazor.Server.Backend.Services;
using AwesomeAssertions;
using Core.Shared.Logging;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Backend.Modules;
using Xunit;

namespace Blazor.Server.Tests.Services;

public class BackendLogServiceTests
{
    private const string AppPath = "AppPath";
    private const string AppLogPath = "AppLogPath";
    private const string OtherLogPath = "OtherLogPath";
    private const string UnknownLogPath = "UnknownLogPath";
    private const string LogText = "Test log content";

    private readonly MockFileSystem _fileSystem;
    private readonly string _appLogPath;
    private readonly string _otherLogPath;
    private readonly string _unknownLogPath;

    private readonly ILogLevelSwitch _logLevelSwitch = Substitute.For<ILogLevelSwitch>();
    private readonly ILogOptions _logOptions = Substitute.For<ILogOptions>();
    private readonly ILogger<BackendLogService> _logger = Substitute.For<ILogger<BackendLogService>>();

    public BackendLogServiceTests()
    {
        var assembly = Assembly.GetAssembly(typeof(BackendModule));
        var binPath = Path.GetDirectoryName(assembly!.Location);
        Assert.NotNull(binPath);

        _fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { @$"./{AppPath}/a.txt", new MockFileData("Demo text content") },
            { @$"./{AppPath}/b.txt", new MockFileData("Demo text content") },
            { @$"./{AppPath}/c.txt", new MockFileData("Demo text content") },
            { @$"./{AppPath}/a/a.txt", new MockFileData("Demo text content") },
            { @$"./{AppPath}/a/b.txt", new MockFileData("Demo text content") },
            { @$"./{AppPath}/a/c.txt", new MockFileData("Demo text content") },
            { @$"./{AppPath}/a/a/a.txt", new MockFileData("Demo text content") },
            { @$"./{AppPath}/a/a/b.txt", new MockFileData("Demo text content") },
            { @$"./{AppPath}/a/a/c.txt", new MockFileData("Demo text content") },
            { @$"./{AppPath}/{AppLogPath}/a.log", new MockFileData("Test log content") },
            { @$"./{AppPath}/{AppLogPath}/b.log", new MockFileData("Test log content") },
            { @$"./{AppPath}/{AppLogPath}/l1/a1.log", new MockFileData(LogText) },
            { @$"./{AppPath}/{AppLogPath}/l1/b1.log", new MockFileData(LogText) },
            { @$"./{AppPath}/{AppLogPath}/l1/c1.log", new MockFileData(LogText) },
            { @$"./{AppPath}/{AppLogPath}/l2/a2.log", new MockFileData(LogText) },
            { @$"./{AppPath}/{AppLogPath}/l3/a3.log", new MockFileData(LogText) },
            { @$"./{AppPath}/{AppLogPath}/l3/b3.log", new MockFileData(LogText) },
            { @$"./tmp/{OtherLogPath}/l1/z1.log", new MockFileData(LogText) },
            { @$"./tmp/{OtherLogPath}/l1/z2.log", new MockFileData(LogText) },
        }, binPath);

        _appLogPath = _fileSystem.Path.Combine(binPath, AppPath, AppLogPath);
        _otherLogPath = _fileSystem.Path.Combine(binPath, "tmp", OtherLogPath);
        _unknownLogPath = _fileSystem.Path.Combine(binPath, "tmp", UnknownLogPath);
    }

    public class GetLogPaths : BackendLogServiceTests
    {
        [Fact]
        public async Task Get_all_log_paths_if_log_path_is_full_qualified()
        {
            // Arrange
            _logOptions.LogPath.Returns(_appLogPath);

            var backendLogService = new BackendLogService(_fileSystem, _logLevelSwitch, _logOptions, _logger);

            // Act
            var logPaths = await backendLogService.GetLogPaths();

            // Asserts
            logPaths.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Get_all_log_paths_if_log_path_is_not_full_qualified()
        {
            // Arrange
            _logOptions.LogPath.Returns(_otherLogPath);

            var backendLogService = new BackendLogService(_fileSystem, _logLevelSwitch, _logOptions, _logger);

            // Act
            var logPaths = await backendLogService.GetLogPaths();

            // Asserts
            logPaths.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Get_all_log_paths_if_log_path_is_unknown()
        {
            // Arrange
            _logOptions.LogPath.Returns(_unknownLogPath);

            var backendLogService = new BackendLogService(_fileSystem, _logLevelSwitch, _logOptions, _logger);

            // Act
            var logPaths = await backendLogService.GetLogPaths();

            // Asserts
            logPaths.Should().BeEmpty();
        }
    }

    public class GetLogTests : BackendLogServiceTests
    {
        [Fact]
        public async Task Returns_logfile_as_stream_with_full_path()
        {
            // Arrange
            _logOptions.LogPath.Returns(_appLogPath);

            var backendLogService = new BackendLogService(_fileSystem, _logLevelSwitch, _logOptions, _logger);

            // Act
            var actualLog = (MockFileStream)await backendLogService.GetLog(_fileSystem.Path.Combine("l1", "a1.log"));

            // Assert
            actualLog.Name.Should().Be(_fileSystem.Path.Combine(_appLogPath, "l1", "a1.log"));
        }

        [Fact]
        public async Task Throws_FileNotFoundException_if_path_is_wrong()
        {
            // Arrange
            _logOptions.LogPath.Returns(_appLogPath);
            var backendLogService = new BackendLogService(_fileSystem, _logLevelSwitch, _logOptions, _logger);

            // Act + Assert
            var act = FluentActions.Awaiting(() => backendLogService.GetLog("z1/y1.log"));
            await act.Should().ThrowAsync<FileNotFoundException>().WithMessage(@"*z1/y1.log*");
        }
    }

    public class LogeLevelTests : BackendLogServiceTests
    {
        [Fact]
        public async Task Set_loglevel_from_log_level_switch()
        {
            // Arrange
            _logOptions.LogPath.Returns(_appLogPath);
            var backendLogService = new BackendLogService(_fileSystem, _logLevelSwitch, _logOptions, _logger);

            // Act 
            var logLevel = LogLevel.Debug;
            await backendLogService.SetLogLevel(logLevel);

            // Assert
            _logLevelSwitch.Received().LogLevel = logLevel;
        }

        [Fact]
        public async Task Returns_loglevel_from_log_level_switch()
        {
            // Arrange
            _logOptions.LogPath.Returns(_appLogPath);
            var backendLogService = new BackendLogService(_fileSystem, _logLevelSwitch, _logOptions, _logger);

            var logLevel = LogLevel.Debug;
            _logLevelSwitch.LogLevel.Returns(logLevel);

            // Act 
            var actualLogLevel = await backendLogService.GetLogLevel();

            // Assert
            actualLogLevel.Should().Be(logLevel);
        }
    }
}
