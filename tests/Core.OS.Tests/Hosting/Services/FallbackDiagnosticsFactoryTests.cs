using System.IO.Abstractions.TestingHelpers;
using Core.OS.EnvironmentOverrides;
using Core.OS.Hosting.Extensions;
using Core.OS.Hosting.Services;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.OS.Tests.EnvironmentOverrides;
using Core.Shared.EnvironmentOverrides;
using Core.Shared.HostManagement;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.OS.Tests.Hosting.Services;

// The page reads the override switch from the process environment, which the whole assembly shares.
[Collection(EnvironmentOverridesCollectionDefinition.Name)]
public sealed class FallbackDiagnosticsFactoryTests : IDisposable
{
    private readonly string? _previousEnv =
        Environment.GetEnvironmentVariable(EnvironmentOverridesSwitch.EnabledEnvironmentVariable);

    private readonly MockFileSystem _fileSystem = new();

    private readonly InstanceOptions _options = new()
    {
        BackupDirectory = "backup",
        CacheDirectory = "cache",
        HomeDirectory = "./home",
        Type = Sdk.Instance.InstanceType.Standalone,
    };

    public FallbackDiagnosticsFactoryTests()
        => _fileSystem.AddDirectory(_options.HomeDirectory);

    public void Dispose()
        => Environment.SetEnvironmentVariable(EnvironmentOverridesSwitch.EnabledEnvironmentVariable, _previousEnv);

    [Fact]
    public async Task Should_report_status_and_messages_from_the_host_options()
    {
        // Arrange
        var options = CreateOptions("Option A is invalid", "Option B is invalid");

        // Act
        var diagnostics = await FallbackDiagnosticsFactory.Create(options, TestContext.Current.CancellationToken);

        // Assert
        diagnostics.Status.Should().Be(FallbackHostStatus.InvalidOptions);
        diagnostics.Messages.Should().Equal("Option A is invalid", "Option B is invalid");
        diagnostics.Version.LocalVersion.Should().NotBeNullOrWhiteSpace();
        diagnostics.Version.SdkVersion.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Should_report_the_persisted_data_version()
    {
        // Arrange
        _fileSystem.AddFile(_fileSystem.GetLocalDataVersionFilePath(_options), new MockFileData("1.3.0"));

        // Act
        var diagnostics = await FallbackDiagnosticsFactory.Create(CreateOptions(), TestContext.Current.CancellationToken);

        // Assert
        diagnostics.Version.DataVersion.Should().Be("1.3.0");
    }

    [Fact]
    public async Task Should_report_no_data_version_when_the_file_is_missing()
    {
        // Arrange - the constructor leaves the home directory empty

        // Act
        var diagnostics = await FallbackDiagnosticsFactory.Create(CreateOptions(), TestContext.Current.CancellationToken);

        // Assert
        diagnostics.Version.DataVersion.Should().BeNull();
    }

    [Fact]
    public async Task Should_report_the_recovery_state()
    {
        // Arrange
        var lastStartup = new DateTimeOffset(2026, 8, 31, 10, 15, 0, TimeSpan.Zero);
        _fileSystem.AddFile(
            _fileSystem.GetLocalRecoveryFilePath(_options),
            new MockFileData($"{{\"lastStartup\":\"{lastStartup:o}\",\"startups\":4,\"recoveryApplied\":true}}"));

        // Act
        var diagnostics = await FallbackDiagnosticsFactory.Create(CreateOptions(), TestContext.Current.CancellationToken);

        // Assert
        diagnostics.Recovery.Should().NotBeNull();
        diagnostics.Recovery!.LastStartup.Should().Be(lastStartup);
        diagnostics.Recovery.Startups.Should().Be(4);
        diagnostics.Recovery.RecoveryApplied.Should().BeTrue();
    }

    [Fact]
    public async Task Should_report_no_recovery_state_when_the_file_is_missing()
    {
        // Arrange - the constructor leaves the home directory empty

        // Act
        var diagnostics = await FallbackDiagnosticsFactory.Create(CreateOptions(), TestContext.Current.CancellationToken);

        // Assert
        diagnostics.Recovery.Should().BeNull();
    }

    [Fact]
    public async Task Should_keep_a_malformed_recovery_file_and_report_no_recovery_state()
    {
        // Arrange
        var recoveryFilePath = _fileSystem.GetLocalRecoveryFilePath(_options);
        _fileSystem.AddFile(recoveryFilePath, new MockFileData("not json"));

        // Act
        var diagnostics = await FallbackDiagnosticsFactory.Create(CreateOptions(), TestContext.Current.CancellationToken);

        // Assert
        diagnostics.Recovery.Should().BeNull();

        // Deleting it would reset the crash cycle from a page request and let the next boot leave
        // the terminal state it was put into on purpose.
        _fileSystem.File.Exists(recoveryFilePath).Should().BeTrue();
    }

    [Fact]
    public async Task Should_report_groups_as_unavailable_when_the_home_directory_is_invalid()
    {
        // Arrange
        var options = new FallbackHostOptions
        {
            Status = FallbackHostStatus.InvalidOptions,
            Messages = ["Instance:HomeDirectory is required"],
            Logger = NullLogger.Instance,
            FileSystem = _fileSystem,
            Instance = new InstanceOptions
            {
                BackupDirectory = "backup",
                CacheDirectory = "cache",
                HomeDirectory = string.Empty,
                Type = Sdk.Instance.InstanceType.Standalone,
            },
        };

        // Act
        var diagnostics = await FallbackDiagnosticsFactory.Create(options, TestContext.Current.CancellationToken);

        // Assert
        diagnostics.Version.LocalVersion.Should().NotBeNullOrWhiteSpace();
        diagnostics.Version.SdkVersion.Should().NotBeNullOrWhiteSpace();
        diagnostics.Version.DataVersion.Should().BeNull();
        diagnostics.Recovery.Should().BeNull();
    }

    [Fact]
    public async Task Should_report_groups_as_unavailable_when_no_context_was_supplied()
    {
        // Arrange
        var options = new FallbackHostOptions
        {
            Status = FallbackHostStatus.RecoveryExhausted,
            Messages = ["Recovery already applied"],
            Logger = NullLogger.Instance,
        };

        // Act
        var diagnostics = await FallbackDiagnosticsFactory.Create(options, TestContext.Current.CancellationToken);

        // Assert
        diagnostics.Version.LocalVersion.Should().NotBeNullOrWhiteSpace();
        diagnostics.Version.SdkVersion.Should().NotBeNullOrWhiteSpace();
        diagnostics.Version.DataVersion.Should().BeNull();
        diagnostics.Recovery.Should().BeNull();
    }

    [Fact]
    public async Task Should_report_environment_overrides_as_switched_off_when_the_switch_is_unset()
    {
        // Arrange
        Environment.SetEnvironmentVariable(EnvironmentOverridesSwitch.EnabledEnvironmentVariable, null);

        // Act
        var diagnostics = await FallbackDiagnosticsFactory.Create(CreateOptions(), TestContext.Current.CancellationToken);

        // Assert
        diagnostics.EnvironmentOverrides.Enabled.Should().BeFalse();
        diagnostics.EnvironmentOverrides.Path.Should().BeNull();
        diagnostics.EnvironmentOverrides.FileExists.Should().BeFalse();
        diagnostics.EnvironmentOverrides.Keys.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_report_the_override_path_when_the_file_is_missing()
    {
        // Arrange
        Environment.SetEnvironmentVariable(EnvironmentOverridesSwitch.EnabledEnvironmentVariable, "true");

        // Act
        var diagnostics = await FallbackDiagnosticsFactory.Create(CreateOptions(), TestContext.Current.CancellationToken);

        // Assert
        diagnostics.EnvironmentOverrides.Enabled.Should().BeTrue();
        diagnostics.EnvironmentOverrides.Path.Should().EndWith("env-overrides.env");
        diagnostics.EnvironmentOverrides.FileExists.Should().BeFalse();
        diagnostics.EnvironmentOverrides.Keys.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_report_the_override_keys_without_their_values()
    {
        // Arrange
        Environment.SetEnvironmentVariable(EnvironmentOverridesSwitch.EnabledEnvironmentVariable, "true");
        WriteOverridesFile(EnvironmentOverridesFormat.Serialize(new Dictionary<string, string>
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "https://collector.example.com",
            ["Authentication__ClientSecret"] = "s3cret",
        }));

        // Act
        var diagnostics = await FallbackDiagnosticsFactory.Create(CreateOptions(), TestContext.Current.CancellationToken);

        // Assert
        diagnostics.EnvironmentOverrides.FileExists.Should().BeTrue();
        diagnostics.EnvironmentOverrides.Error.Should().BeNull();
        diagnostics.EnvironmentOverrides.Keys.Should().BeEquivalentTo(
            ["Authentication__ClientSecret", "OTEL_EXPORTER_OTLP_ENDPOINT"]);
    }

    [Fact]
    public async Task Should_report_a_malformed_override_file_as_an_error_without_quoting_it()
    {
        // Arrange
        Environment.SetEnvironmentVariable(EnvironmentOverridesSwitch.EnabledEnvironmentVariable, "true");
        WriteOverridesFile("OTEL_EXPORTER_OTLP_ENDPOINT=https://collector.example.com\n");

        // Act
        var diagnostics = await FallbackDiagnosticsFactory.Create(CreateOptions(), TestContext.Current.CancellationToken);

        // Assert
        diagnostics.EnvironmentOverrides.FileExists.Should().BeTrue();
        diagnostics.EnvironmentOverrides.Keys.Should().BeEmpty();
        diagnostics.EnvironmentOverrides.Error.Should().Contain("malformed");
        diagnostics.EnvironmentOverrides.Error.Should().NotContain("collector.example.com");
    }

    [Fact]
    public async Task Should_report_the_override_group_as_unreadable_when_the_home_directory_does_not_exist()
    {
        // Arrange
        Environment.SetEnvironmentVariable(EnvironmentOverridesSwitch.EnabledEnvironmentVariable, "true");
        var options = new FallbackHostOptions
        {
            Status = FallbackHostStatus.InvalidOptions,
            Messages = ["Instance:HomeDirectory does not exist"],
            Logger = NullLogger.Instance,
            FileSystem = _fileSystem,
            Instance = new InstanceOptions
            {
                BackupDirectory = "backup",
                CacheDirectory = "cache",
                HomeDirectory = "./does-not-exist",
                Type = Sdk.Instance.InstanceType.Standalone,
            },
        };

        // Act
        var diagnostics = await FallbackDiagnosticsFactory.Create(options, TestContext.Current.CancellationToken);

        // Assert
        diagnostics.EnvironmentOverrides.Enabled.Should().BeTrue();
        diagnostics.EnvironmentOverrides.Path.Should().BeNull();
        diagnostics.EnvironmentOverrides.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_offer_the_disable_action_when_the_file_is_there_and_a_restart_can_be_requested()
    {
        // Arrange
        Environment.SetEnvironmentVariable(EnvironmentOverridesSwitch.EnabledEnvironmentVariable, "true");
        WriteOverridesFile(EnvironmentOverridesFormat.Serialize(new Dictionary<string, string> { ["A"] = "b" }));

        // Act
        var diagnostics = await FallbackDiagnosticsFactory.Create(
            CreateOptionsWithHostManagement(),
            TestContext.Current.CancellationToken);

        // Assert
        diagnostics.EnvironmentOverrides.CanDisable.Should().BeTrue();
    }

    [Fact]
    public async Task Should_offer_the_disable_action_for_a_malformed_file()
    {
        // Arrange - a file the page cannot parse is exactly the one an operator needs to get rid
        // of, so the unreadable file must not take the button away with it.
        Environment.SetEnvironmentVariable(EnvironmentOverridesSwitch.EnabledEnvironmentVariable, "true");
        WriteOverridesFile("OTEL_EXPORTER_OTLP_ENDPOINT=https://collector.example.com\n");

        // Act
        var diagnostics = await FallbackDiagnosticsFactory.Create(
            CreateOptionsWithHostManagement(),
            TestContext.Current.CancellationToken);

        // Assert
        diagnostics.EnvironmentOverrides.Error.Should().NotBeNull();
        diagnostics.EnvironmentOverrides.CanDisable.Should().BeTrue();
    }

    [Fact]
    public async Task Should_not_offer_the_disable_action_when_there_is_no_file_to_disable()
    {
        // Arrange
        Environment.SetEnvironmentVariable(EnvironmentOverridesSwitch.EnabledEnvironmentVariable, "true");

        // Act
        var diagnostics = await FallbackDiagnosticsFactory.Create(
            CreateOptionsWithHostManagement(),
            TestContext.Current.CancellationToken);

        // Assert
        diagnostics.EnvironmentOverrides.FileExists.Should().BeFalse();
        diagnostics.EnvironmentOverrides.CanDisable.Should().BeFalse();
    }

    [Fact]
    public async Task Should_not_offer_the_disable_action_without_host_management_options()
    {
        // Arrange - the action restarts the instance afterwards, which is what host management is
        // needed for; offering it without one would leave the instance down.
        Environment.SetEnvironmentVariable(EnvironmentOverridesSwitch.EnabledEnvironmentVariable, "true");
        WriteOverridesFile(EnvironmentOverridesFormat.Serialize(new Dictionary<string, string> { ["A"] = "b" }));

        // Act
        var diagnostics = await FallbackDiagnosticsFactory.Create(CreateOptions(), TestContext.Current.CancellationToken);

        // Assert
        diagnostics.EnvironmentOverrides.FileExists.Should().BeTrue();
        diagnostics.EnvironmentOverrides.CanDisable.Should().BeFalse();
    }

    private void WriteOverridesFile(string contents)
        => _fileSystem.AddFile(
            EnvironmentOverridesFile.RequirePath(_fileSystem, _options.HomeDirectory),
            new MockFileData(contents));

    /// <summary>
    /// The disable action restarts the instance afterwards, which is what host management is for.
    /// </summary>
    private FallbackHostOptions CreateOptionsWithHostManagement() => new()
    {
        Status = FallbackHostStatus.InvalidOptions,
        Messages = [],
        Logger = NullLogger.Instance,
        FileSystem = _fileSystem,
        Instance = _options,
        HostManagement = new HostManagementOptions(),
    };

    private FallbackHostOptions CreateOptions(params string[] messages) => new()
    {
        Status = FallbackHostStatus.InvalidOptions,
        Messages = messages,
        Logger = NullLogger.Instance,
        FileSystem = _fileSystem,
        Instance = _options,
    };
}
