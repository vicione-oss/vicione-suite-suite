using Core.OS.Instance;
using Core.OS.Instance.Consumers;
using Core.Shared.Persistence.Commands;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Instance.Consumers;

public sealed class CreateBackupConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IBackupFactory _factory = Substitute.For<IBackupFactory>();
    private readonly IBackupStore _store = Substitute.For<IBackupStore>();

    public CreateBackupConsumerTests()
    {
        _configureServices = cfg =>
        {
            cfg.AddConsumer<CreateBackupConsumer>();
            cfg.AddSingleton(_factory);
            cfg.AddSingleton(_store);
        };
    }

    [Fact]
    public async Task Should_create_backup()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new CreateBackup();

        // Act
        await tester.TestCommand<CreateBackup, CreateBackupConsumer>(command);

        // Assert
        _store.Received(1).CreateBackupFile(out Arg.Any<string>());
        await _factory.Received(1).CreateBackup(Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_publish_event_with_error_info_on_exception()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new CreateBackup();

        // Act
        await tester.TestCommand<CreateBackup, CreateBackupConsumer>(command);

        // Assert
        _store.Received(1).CreateBackupFile(out Arg.Any<string>());
        await _factory.Received(1).CreateBackup(Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }
}
