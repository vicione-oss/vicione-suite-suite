using Core.OS.Instance;
using Core.OS.Persistence;
using Core.OS.Persistence.Consumers;
using Core.OS.Instance.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Sdk.Instance;

namespace Core.OS.Tests.Persistence;

public partial class DbChangeSetConsumerTests
{
    public sealed class GapDetection : DbChangeSetConsumerTests
    {
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly ReplicationSequenceTracker _tracker;
        private readonly ILocalInstanceInformationProvider _localInstanceInfo = Substitute.For<ILocalInstanceInformationProvider>();
        private readonly IServiceProvider _services = Substitute.For<IServiceProvider>();
        private readonly SynchronizationState _synchronizationState = new();

        public GapDetection()
        {
            _tracker = new ReplicationSequenceTracker(_timeProvider);
            _synchronizationState.CompleteSynchronization();

            var instanceInfo = Substitute.For<IInstanceInformation>();
            instanceInfo.Id.Returns(Guid.NewGuid());
            instanceInfo.Type.Returns(InstanceType.Slave);
            instanceInfo.Name.Returns("TestSlave");
            instanceInfo.SerialNumber.Returns("SN001");
            instanceInfo.SystemType.Returns("Edge-S");
            instanceInfo.SdkVersion.Returns("1.0.0");
            instanceInfo.Version.Returns("1.0.0");
            _localInstanceInfo.Local.Returns(instanceInfo);
            _localInstanceInfo.LoadedModules.Returns(new List<string> { "TestModule" });
        }

        [Fact]
        public async Task Should_buffer_out_of_order_message_without_triggering_sync()
        {
            // Arrange
            var consumer = new DbChangeSetConsumer(_services, NullLogger<DbChangeSetConsumer>.Instance, _tracker, new ReplicationLagTracker(), _localInstanceInfo, _synchronizationState);
            await consumer.Consume(CreateConsumeContext(new DbChangeSet([], "TestContext", 1, DateTimeOffset.UtcNow)));

            // Act — receive seq 3 before seq 2
            var ctx3 = CreateConsumeContext(new DbChangeSet([], "TestContext", 3, DateTimeOffset.UtcNow));
            await consumer.Consume(ctx3);

            // Assert — no full-sync triggered, message buffered
            await ctx3.DidNotReceive().GetSendEndpoint(Arg.Any<Uri>());
            _tracker.GetBufferedCount("TestContext").Should().Be(1);
        }

        [Fact]
        public async Task Should_apply_buffered_when_gap_filled()
        {
            // Arrange
            var consumer = new DbChangeSetConsumer(_services, NullLogger<DbChangeSetConsumer>.Instance, _tracker, new ReplicationLagTracker(), _localInstanceInfo, _synchronizationState);
            await consumer.Consume(CreateConsumeContext(new DbChangeSet([], "TestContext", 1, DateTimeOffset.UtcNow)));
            await consumer.Consume(CreateConsumeContext(new DbChangeSet([], "TestContext", 3, DateTimeOffset.UtcNow)));

            // Act — fill the gap
            var ctx2 = CreateConsumeContext(new DbChangeSet([], "TestContext", 2, DateTimeOffset.UtcNow));
            await consumer.Consume(ctx2);

            // Assert — buffer drained, lastApplied advanced, no sync
            await ctx2.DidNotReceive().GetSendEndpoint(Arg.Any<Uri>());
            _tracker.GetLastApplied("TestContext").Should().Be(3);
            _tracker.GetBufferedCount("TestContext").Should().Be(0);
        }

        [Fact]
        public async Task Should_trigger_full_sync_on_buffer_timeout()
        {
            // Arrange
            var consumer = new DbChangeSetConsumer(_services, NullLogger<DbChangeSetConsumer>.Instance, _tracker, new ReplicationLagTracker(), _localInstanceInfo, _synchronizationState);
            await consumer.Consume(CreateConsumeContext(new DbChangeSet([], "TestContext", 1, DateTimeOffset.UtcNow)));
            await consumer.Consume(CreateConsumeContext(new DbChangeSet([], "TestContext", 3, DateTimeOffset.UtcNow)));

            // Advance time past the timeout
            _timeProvider.Advance(TimeSpan.FromSeconds(5));

            // Act — next out-of-order message triggers timeout
            var ctx4 = CreateConsumeContext(new DbChangeSet([], "TestContext", 4, DateTimeOffset.UtcNow));
            await consumer.Consume(ctx4);

            // Assert — full-sync triggered
            await ctx4.Received(1).GetSendEndpoint(Arg.Any<Uri>());
        }

        [Fact]
        public async Task Should_not_trigger_full_sync_on_contiguous_sequence()
        {
            // Arrange
            var consumer = new DbChangeSetConsumer(_services, NullLogger<DbChangeSetConsumer>.Instance, _tracker, new ReplicationLagTracker(), _localInstanceInfo, _synchronizationState);

            // Act
            var ctx1 = CreateConsumeContext(new DbChangeSet([], "TestContext", 1, DateTimeOffset.UtcNow));
            await consumer.Consume(ctx1);
            var ctx2 = CreateConsumeContext(new DbChangeSet([], "TestContext", 2, DateTimeOffset.UtcNow));
            await consumer.Consume(ctx2);

            // Assert
            await ctx1.DidNotReceive().GetSendEndpoint(Arg.Any<Uri>());
            await ctx2.DidNotReceive().GetSendEndpoint(Arg.Any<Uri>());
        }

        [Fact]
        public async Task Should_accept_first_message_with_any_sequence()
        {
            // Arrange
            var consumer = new DbChangeSetConsumer(_services, NullLogger<DbChangeSetConsumer>.Instance, _tracker, new ReplicationLagTracker(), _localInstanceInfo, _synchronizationState);

            // Act
            var context = CreateConsumeContext(new DbChangeSet([], "TestContext", 100, DateTimeOffset.UtcNow));
            await consumer.Consume(context);

            // Assert
            await context.DidNotReceive().GetSendEndpoint(Arg.Any<Uri>());
            _tracker.GetLastApplied("TestContext").Should().Be(100);
        }

        [Fact]
        public async Task Should_accept_messages_after_tracker_reset()
        {
            // Arrange
            var consumer = new DbChangeSetConsumer(_services, NullLogger<DbChangeSetConsumer>.Instance, _tracker, new ReplicationLagTracker(), _localInstanceInfo, _synchronizationState);
            await consumer.Consume(CreateConsumeContext(new DbChangeSet([], "TestContext", 1, DateTimeOffset.UtcNow)));
            _tracker.Reset();

            // Act — master's counter continued
            var ctx = CreateConsumeContext(new DbChangeSet([], "TestContext", 50, DateTimeOffset.UtcNow));
            await consumer.Consume(ctx);

            // Assert
            await ctx.DidNotReceive().GetSendEndpoint(Arg.Any<Uri>());
            _tracker.GetLastApplied("TestContext").Should().Be(50);
        }

        [Fact]
        public async Task Should_skip_duplicate_message()
        {
            // Arrange
            var consumer = new DbChangeSetConsumer(_services, NullLogger<DbChangeSetConsumer>.Instance, _tracker, new ReplicationLagTracker(), _localInstanceInfo, _synchronizationState);
            await consumer.Consume(CreateConsumeContext(new DbChangeSet([], "TestContext", 1, DateTimeOffset.UtcNow)));

            // Act — redelivery
            var ctx = CreateConsumeContext(new DbChangeSet([], "TestContext", 1, DateTimeOffset.UtcNow));
            await consumer.Consume(ctx);

            // Assert
            await ctx.DidNotReceive().GetSendEndpoint(Arg.Any<Uri>());
        }
    }
}
