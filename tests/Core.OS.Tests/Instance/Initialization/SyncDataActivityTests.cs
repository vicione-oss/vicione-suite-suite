using Core.OS.Instance.Initialization;
using Core.OS.Instance.Services;
using Core.OS.Persistence;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.OS.Tests.Instance.Initialization;

public class SyncDataActivityTests
{
    [Fact(Skip = "TODO - this functionality needs to tested again a real postgres db")]
    public void To_be_tested()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public async Task Should_notify_the_replication_observers_when_the_full_sync_completes()
    {
        // Arrange
        var observer = Substitute.For<IReplicationObserver>();
        await using var services = new ServiceCollection()
            .AddSingleton(new ReplicationSequenceTracker())
            .AddSingleton(new SyncRetryState())
            .AddSingleton(new SynchronizationState())
            .AddSingleton(observer)
            .BuildServiceProvider();

        var context = Substitute.For<ExecuteContext<SyncDataArguments>>();
        context.Arguments.Returns(new SyncDataArguments { SyncCompleted = true });

        var sut = new SyncDataActivity(services, NullLogger<SyncDataActivity>.Instance);

        // Act
        await sut.Execute(context);

        // Assert
        observer.Received(1).Resynchronized();
    }
}
