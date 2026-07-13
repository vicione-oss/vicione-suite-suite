using Core.OS.Instance;
using Core.OS.Instance.Consumers;
using Core.Shared.Persistence.Contracts;
using Core.Shared.Persistence.Requests;
using AwesomeAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Instance.Consumers;

public sealed class GetBackupConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IBackupStore _backupStore = Substitute.For<IBackupStore>();
    private readonly byte[] _backupBytes = [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07];

    public GetBackupConsumerTests()
        => _configureServices = cfg =>
        {
            // consumer needs
            cfg.AddConsumer<GetBackupConsumer>();
            cfg.AddSingleton(Substitute.For<ILogger<GetBackupConsumer>>);
            cfg.AddSingleton(_backupStore);
        };

    [Fact]
    public async Task Should_return_error_when_no_backup_exists()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetBackup();

        // Act
        var response = await tester.TestRequest<GetBackupResponse, GetBackup>(request);

        // Assert
        response.Should().NotBeNull();
        response.RequestError.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_return_data_for_requested_filename()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var fileName = "File2";
        _backupStore.ReadBackupFile(fileName).Returns(new MemoryStream(_backupBytes));
        var request = new GetBackup(fileName);

        // Act
        var response = await tester.TestRequest<GetBackupResponse, GetBackup>(request);

        // Assert
        response.Should().NotBeNull();
        response.RequestError.Should().BeNull();
        response.FileName.Should().Be(GetBackupConsumer.GetResponseFileName(fileName));
        response.Content.Should().BeEquivalentTo(_backupBytes);
    }

    [Fact]
    public async Task Should_return_data_for_most_recent_backup()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var fileName = "File2";
        _backupStore.GetLatestBackup().Returns(new BackupFileInfo(fileName, DateTimeOffset.UtcNow, 42000));
        _backupStore.ReadBackupFile(fileName).Returns(new MemoryStream(_backupBytes));
        var request = new GetBackup();

        // Act
        var response = await tester.TestRequest<GetBackupResponse, GetBackup>(request);

        // Assert
        response.Should().NotBeNull();
        response.RequestError.Should().BeNull();
        response.FileName.Should().Be(GetBackupConsumer.GetResponseFileName(fileName));
        response.Content.Should().BeEquivalentTo(_backupBytes);
    }
}
