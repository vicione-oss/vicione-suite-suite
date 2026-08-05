using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Core.OS.HostManagement;
using Core.OS.Instance;
using Core.OS.Instance.Consumers;
using Core.OS.Instance.Extensions;
using Core.OS.Instance.Services;
using Core.OS.Tests.Extensions;
using Core.OS.Tests.HostManagement.Extensions;
using Core.OS.Tests.Persistence;
using Core.Shared.Instance.Commands;
using Core.Shared.Persistence.Commands;
using Core.Shared.Persistence.Events;
using HostManagement.Shared.Communication;
using HostManagement.Shared.Communication.Enums;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sdk.Backend.Modules;
using Sdk.Instance;
using Sdk.SystemConfiguration.Commands;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.Instance.Consumers;

public sealed class RestoreBackupConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IBackupStore _store = Substitute.For<IBackupStore>();
    private readonly ILocalInstanceInformationProvider _informationProvider = Substitute.For<ILocalInstanceInformationProvider>();
    private readonly MockFileSystem _fileSystem = new();
    private readonly InstanceOptions _instanceOptions;
    private readonly IPipeClient _pipeClient = Substitute.For<IPipeClient>();
    private readonly IModuleHostRequestHandler _handler1 = Substitute.For<IModuleHostRequestHandler>();
    private readonly IModuleHostRequestHandler _handler2 = Substitute.For<IModuleHostRequestHandler>();

    public RestoreBackupConsumerTests()
    {
        _instanceOptions = new InstanceOptions
        {
            HomeDirectory = _fileSystem.Path.GetFullPath("AppData"),
            CacheDirectory = _fileSystem.Path.GetFullPath("Cache"),
            BackupDirectory = _fileSystem.Path.GetFullPath("Backup"),
            Type = InstanceType.Standalone,
        };

        _fileSystem.AddDirectory(_instanceOptions.BackupDirectory);
        _fileSystem.AddDirectory(_instanceOptions.HomeDirectory);

        _configureServices = cfg =>
        {
            cfg.AddConsumer<RestoreBackupConsumer>();
            cfg.AddSingleton(_informationProvider);
            cfg.AddSingleton<IFileSystem>(_fileSystem);
            cfg.AddSingleton(_store);
            cfg.AddSingleton(_pipeClient);
            cfg.AddSingleton(Options.Create(_instanceOptions));
            cfg.AddSingleton(_handler1);
            cfg.AddSingleton(_handler2);
        };
    }

    [Fact]
    public async Task Should_do_nothing_if_no_restore_flag_is_set()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var backupZipBytes = TestResources.GetEmbeddedBackupFileBytes();
        var instanceId = await GetInstanceIdFromArchive(backupZipBytes);
        _informationProvider.SetupLocalInstanceInformation(guid: instanceId);

        var command = new RestoreBackup
        {
            BackupFilePath = WriteUploadedBackup(backupZipBytes),
        };

        // Act
        await tester.TestCommand<RestoreBackup, RestoreBackupConsumer>(command);

        // Assert
        _store.Received(0).CreateBackupFile(out Arg.Any<string>());
        (await _fileSystem.ReadRestoreTask(_instanceOptions, TestContext.Current.CancellationToken)).Should().BeNull();
        (await tester.Harness.Sent.Any<ControlService>(k => k.Context.Message.ServiceName == _instanceOptions.ServiceName, TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task Should_prepare_restore_suite_configuration()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        SetupBackupStoreCreateBackupFile();
        var backupZipBytes = TestResources.GetEmbeddedBackupFileBytes();
        var instanceId = await GetInstanceIdFromArchive(backupZipBytes);
        _informationProvider.SetupLocalInstanceInformation(guid: instanceId);

        var command = new RestoreBackup
        {
            BackupFilePath = WriteUploadedBackup(backupZipBytes),
            SuiteConfiguration = true,
        };

        // Act
        var response = await tester.TestCommand<RestoreBackup, RestoreBackupConsumer, RestoreBackupPrepared>(command);

        // Assert
        _store.Received(1).CreateBackupFile(out Arg.Any<string>());
        (await _fileSystem.ReadRestoreTask(_instanceOptions, TestContext.Current.CancellationToken)).Should().NotBeNull();
        response.ErrorInfo.Should().BeNull();
    }

    [Fact]
    public async Task Should_not_prepare_restore_suite_configuration_if_command_flag_not_set()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        SetupBackupStoreCreateBackupFile();
        var backupZipBytes = TestResources.GetEmbeddedBackupFileBytes();
        var instanceId = await GetInstanceIdFromArchive(backupZipBytes);
        _informationProvider.SetupLocalInstanceInformation(guid: instanceId);

        var command = new RestoreBackup
        {
            BackupFilePath = WriteUploadedBackup(backupZipBytes),
            SuiteConfiguration = false,
            SystemConfiguration = true,
        };

        // Act
        var response = await tester.TestCommand<RestoreBackup, RestoreBackupConsumer, RestoreBackupPrepared>(command);

        // Assert
        _store.Received(0).CreateBackupFile(out Arg.Any<string>());
        (await _fileSystem.ReadRestoreTask(_instanceOptions, TestContext.Current.CancellationToken)).Should().BeNull();
        response.ErrorInfo.Should().BeNull();
    }

    [Fact]
    public async Task Should_restart_suite_after_preparing_restore_configuration()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        SetupBackupStoreCreateBackupFile();
        var backupZipBytes = TestResources.GetEmbeddedBackupFileBytes();
        var instanceId = await GetInstanceIdFromArchive(backupZipBytes);
        _informationProvider.SetupLocalInstanceInformation(guid: instanceId);

        var command = new RestoreBackup
        {
            BackupFilePath = WriteUploadedBackup(backupZipBytes),
            SuiteConfiguration = true,
        };

        // Act
        await tester.TestCommand<RestoreBackup, RestoreBackupConsumer>(command);

        // Assert
        (await tester.Harness.Sent.Any<ControlInstance>(k => k.Context.Message.InstanceId == instanceId
            && k.Context.Message.Action == InstanceCommand.Restart, TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task Should_call_module_host_request_handlers_on_restore()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        SetupBackupStoreCreateBackupFile();
        var backupZipBytes = TestResources.GetEmbeddedBackupFileBytes();
        var instanceId = await GetInstanceIdFromArchive(backupZipBytes);
        _informationProvider.SetupLocalInstanceInformation(guid: instanceId);
        _pipeClient.SetupSetSystemConfigurationResult(OperationStatus.Success);

        var command = new RestoreBackup
        {
            BackupFilePath = WriteUploadedBackup(backupZipBytes),
            SuiteConfiguration = true,
        };

        // Act
        var response = await tester.TestCommand<RestoreBackup, RestoreBackupConsumer, RestoreBackupPrepared>(command);

        // Assert
        await _handler1.Received(1).OnRestore(Arg.Any<CancellationToken>());
        await _handler2.Received(1).OnRestore(Arg.Any<CancellationToken>());
        response.ErrorInfo.Should().BeNull();
    }

    [Fact]
    public async Task Should_set_system_configuration_to_host_management()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var backupZipBytes = TestResources.GetEmbeddedBackupFileBytes();
        var instanceId = await GetInstanceIdFromArchive(backupZipBytes);
        _informationProvider.SetupLocalInstanceInformation(guid: instanceId);
        _pipeClient.SetupSetSystemConfigurationResult(OperationStatus.Success);
        _pipeClient.SetupGetSystemConfigurationResult(OperationStatus.Success);

        var command = new RestoreBackup
        {
            BackupFilePath = WriteUploadedBackup(backupZipBytes),
            SystemConfiguration = true,
        };

        // Act
        var response = await tester.TestCommand<RestoreBackup, RestoreBackupConsumer, RestoreBackupPrepared>(command);

        // Assert
        await _pipeClient.Received(1).SendRequest(Topics.SetSystemConfiguration, Arg.Any<string>(), Arg.Any<CancellationToken>());
        response.ErrorInfo.Should().BeNull();
    }

    [Fact]
    public async Task Should_not_set_system_configuration_to_host_management_if_command_flag_not_set()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        SetupBackupStoreCreateBackupFile();
        var backupZipBytes = TestResources.GetEmbeddedBackupFileBytes();
        var instanceId = await GetInstanceIdFromArchive(backupZipBytes);
        _informationProvider.SetupLocalInstanceInformation(guid: instanceId);
        _pipeClient.SetupSetSystemConfigurationResult(OperationStatus.Success);
        _pipeClient.SetupGetSystemConfigurationResult(OperationStatus.Success);

        var command = new RestoreBackup
        {
            BackupFilePath = WriteUploadedBackup(backupZipBytes),
            SuiteConfiguration = true,
            SystemConfiguration = false,
        };

        // Act
        var response = await tester.TestCommand<RestoreBackup, RestoreBackupConsumer, RestoreBackupPrepared>(command);

        // Assert
        await _pipeClient.Received(0).SendRequest(Topics.SetSystemConfiguration, Arg.Any<string>(), Arg.Any<CancellationToken>());
        response.ErrorInfo.Should().BeNull();
    }

    [Fact]
    public async Task Should_publish_event_with_error_info_on_exception()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var backupZipBytes = TestResources.GetEmbeddedBackupFileBytes();

        var command = new RestoreBackup
        {
            BackupFilePath = WriteUploadedBackup(backupZipBytes),
            SuiteConfiguration = true,
        };

        // Act
        var response = await tester.TestCommand<RestoreBackup, RestoreBackupConsumer, RestoreBackupPrepared>(command);

        // Assert
        (await _fileSystem.ReadRestoreTask(_instanceOptions, TestContext.Current.CancellationToken)).Should().BeNull();
        response.ErrorInfo.Should().NotBeNull();
    }

    private static async Task<Guid> GetInstanceIdFromArchive(byte[] zipContent)
    {
        using var memoryStream = new MemoryStream(zipContent);
        var metadata = await BackupReader.GetBackupMetadata(memoryStream);

        return metadata.InstanceId;
    }

    private void SetupBackupStoreCreateBackupFile()
        => _store.CreateBackupFile(out _)
            .Returns(x =>
            {
                x[0] = "backup.zip";
                return new MemoryStream();
            });

    private string WriteUploadedBackup(byte[] bytes)
    {
        var path = _fileSystem.Path.Combine(_instanceOptions.CacheDirectory, Core.Shared.Constants.BackupFileName);
        _fileSystem.AddFile(path, new MockFileData(bytes));

        return path;
    }
}
