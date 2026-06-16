using Core.OS.Persistence;
using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Core.OS.Tests.Persistence;

public class ReplicationSequenceTrackerTests
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly ReplicationSequenceTracker _tracker;

    public ReplicationSequenceTrackerTests()
    {
        _tracker = new ReplicationSequenceTracker(_timeProvider);
    }

    [Fact]
    public void Submit_should_apply_first_message_with_any_sequence()
    {
        // Arrange
        var msg = new DbChangeSet([], "TestContext", 42, DateTimeOffset.UtcNow);

        // Act
        var result = _tracker.Submit("TestContext", 42, msg);

        // Assert
        result.MessagesToApply.Should().HaveCount(1);
        result.MessagesToApply[0].Should().BeSameAs(msg);
        result.RequiresFullSync.Should().BeFalse();
        _tracker.GetLastApplied("TestContext").Should().Be(42);
    }

    [Fact]
    public void Submit_should_apply_contiguous_sequence()
    {
        // Arrange
        _tracker.Submit("TestContext", 1, new DbChangeSet([], "TestContext", 1, DateTimeOffset.UtcNow));

        // Act
        var msg = new DbChangeSet([], "TestContext", 2, DateTimeOffset.UtcNow);
        var result = _tracker.Submit("TestContext", 2, msg);

        // Assert
        result.MessagesToApply.Should().HaveCount(1);
        result.MessagesToApply[0].Should().BeSameAs(msg);
        result.RequiresFullSync.Should().BeFalse();
        _tracker.GetLastApplied("TestContext").Should().Be(2);
    }

    [Fact]
    public void Submit_should_buffer_when_gap_detected()
    {
        // Arrange
        _tracker.Submit("TestContext", 1, new DbChangeSet([], "TestContext", 1, DateTimeOffset.UtcNow));

        // Act — skip sequence 2, receive 3
        var result = _tracker.Submit("TestContext", 3, new DbChangeSet([], "TestContext", 3, DateTimeOffset.UtcNow));

        // Assert
        result.MessagesToApply.Should().BeEmpty();
        result.WasBuffered.Should().BeTrue();
        result.RequiresFullSync.Should().BeFalse();
        _tracker.GetLastApplied("TestContext").Should().Be(1);
        _tracker.GetBufferedCount("TestContext").Should().Be(1);
    }

    [Fact]
    public void Submit_should_skip_duplicate_sequence()
    {
        // Arrange
        _tracker.Submit("TestContext", 5, new DbChangeSet([], "TestContext", 5, DateTimeOffset.UtcNow));

        // Act
        var result = _tracker.Submit("TestContext", 5, new DbChangeSet([], "TestContext", 5, DateTimeOffset.UtcNow));

        // Assert
        result.MessagesToApply.Should().BeEmpty();
        result.WasBuffered.Should().BeFalse();
        result.RequiresFullSync.Should().BeFalse();
    }

    [Fact]
    public void Submit_should_skip_older_sequence()
    {
        // Arrange
        _tracker.Submit("TestContext", 1, new DbChangeSet([], "TestContext", 1, DateTimeOffset.UtcNow));
        _tracker.Submit("TestContext", 2, new DbChangeSet([], "TestContext", 2, DateTimeOffset.UtcNow));
        _tracker.Submit("TestContext", 3, new DbChangeSet([], "TestContext", 3, DateTimeOffset.UtcNow));

        // Act
        var result = _tracker.Submit("TestContext", 1, new DbChangeSet([], "TestContext", 1, DateTimeOffset.UtcNow));

        // Assert
        result.MessagesToApply.Should().BeEmpty();
        _tracker.GetLastApplied("TestContext").Should().Be(3);
    }

    [Fact]
    public void Submit_should_track_contexts_independently()
    {
        // Arrange
        _tracker.Submit("ContextA", 1, new DbChangeSet([], "ContextA", 1, DateTimeOffset.UtcNow));
        _tracker.Submit("ContextB", 10, new DbChangeSet([], "ContextB", 10, DateTimeOffset.UtcNow));

        // Act
        var resultA = _tracker.Submit("ContextA", 2, new DbChangeSet([], "ContextA", 2, DateTimeOffset.UtcNow));
        var resultB = _tracker.Submit("ContextB", 11, new DbChangeSet([], "ContextB", 11, DateTimeOffset.UtcNow));

        // Assert
        resultA.MessagesToApply.Should().HaveCount(1);
        resultB.MessagesToApply.Should().HaveCount(1);
    }

    [Fact]
    public void Submit_should_drain_buffer_when_gap_filled()
    {
        // Arrange
        _tracker.Submit("TestContext", 1, new DbChangeSet([], "TestContext", 1, DateTimeOffset.UtcNow));
        var msg3 = new DbChangeSet([], "TestContext", 3, DateTimeOffset.UtcNow);
        var msg4 = new DbChangeSet([], "TestContext", 4, DateTimeOffset.UtcNow);
        _tracker.Submit("TestContext", 3, msg3);
        _tracker.Submit("TestContext", 4, msg4);

        // Act — fill the gap
        var msg2 = new DbChangeSet([], "TestContext", 2, DateTimeOffset.UtcNow);
        var result = _tracker.Submit("TestContext", 2, msg2);

        // Assert — returns msg2 + drained msg3, msg4
        result.MessagesToApply.Should().HaveCount(3);
        result.MessagesToApply[0].Should().BeSameAs(msg2);
        result.MessagesToApply[1].Should().BeSameAs(msg3);
        result.MessagesToApply[2].Should().BeSameAs(msg4);
        result.RequiresFullSync.Should().BeFalse();
        _tracker.GetLastApplied("TestContext").Should().Be(4);
        _tracker.GetBufferedCount("TestContext").Should().Be(0);
    }

    [Fact]
    public void Submit_should_drain_only_up_to_next_gap()
    {
        // Arrange
        _tracker.Submit("TestContext", 1, new DbChangeSet([], "TestContext", 1, DateTimeOffset.UtcNow));
        var msg3 = new DbChangeSet([], "TestContext", 3, DateTimeOffset.UtcNow);
        var msg5 = new DbChangeSet([], "TestContext", 5, DateTimeOffset.UtcNow);
        _tracker.Submit("TestContext", 3, msg3);
        _tracker.Submit("TestContext", 5, msg5);

        // Act — fill first gap only
        var msg2 = new DbChangeSet([], "TestContext", 2, DateTimeOffset.UtcNow);
        var result = _tracker.Submit("TestContext", 2, msg2);

        // Assert — msg2 + msg3 drained, msg5 still buffered (gap at 4)
        result.MessagesToApply.Should().HaveCount(2);
        result.MessagesToApply[0].Should().BeSameAs(msg2);
        result.MessagesToApply[1].Should().BeSameAs(msg3);
        _tracker.GetLastApplied("TestContext").Should().Be(3);
        _tracker.GetBufferedCount("TestContext").Should().Be(1);
    }

    [Fact]
    public void Submit_should_not_flush_before_timeout()
    {
        // Arrange
        _tracker.Submit("TestContext", 1, new DbChangeSet([], "TestContext", 1, DateTimeOffset.UtcNow));
        _tracker.Submit("TestContext", 3, new DbChangeSet([], "TestContext", 3, DateTimeOffset.UtcNow));
        _timeProvider.Advance(TimeSpan.FromSeconds(4)); // Less than 5s

        // Act — another out-of-order message
        var result = _tracker.Submit("TestContext", 4, new DbChangeSet([], "TestContext", 4, DateTimeOffset.UtcNow));

        // Assert — still buffered, no flush
        result.WasBuffered.Should().BeTrue();
        result.RequiresFullSync.Should().BeFalse();
        _tracker.GetBufferedCount("TestContext").Should().Be(2);
    }

    [Fact]
    public void Submit_should_flush_and_require_resync_after_timeout()
    {
        // Arrange
        _tracker.Submit("TestContext", 1, new DbChangeSet([], "TestContext", 1, DateTimeOffset.UtcNow));
        var msg3 = new DbChangeSet([], "TestContext", 3, DateTimeOffset.UtcNow);
        _tracker.Submit("TestContext", 3, msg3);
        _timeProvider.Advance(TimeSpan.FromSeconds(5)); // At timeout

        // Act — another out-of-order message triggers timeout check
        var msg4 = new DbChangeSet([], "TestContext", 4, DateTimeOffset.UtcNow);
        var result = _tracker.Submit("TestContext", 4, msg4);

        // Assert — flushed in order, full-sync required
        result.RequiresFullSync.Should().BeTrue();
        result.MessagesToApply.Should().HaveCount(2);
        result.MessagesToApply[0].Should().BeSameAs(msg3);
        result.MessagesToApply[1].Should().BeSameAs(msg4);
        _tracker.GetBufferedCount("TestContext").Should().Be(0);
    }

    [Fact]
    public void Reset_should_clear_all_contexts()
    {
        // Arrange
        _tracker.Submit("ContextA", 5, new DbChangeSet([], "ContextA", 5, DateTimeOffset.UtcNow));
        _tracker.Submit("ContextB", 10, new DbChangeSet([], "ContextB", 10, DateTimeOffset.UtcNow));
        _tracker.Submit("ContextA", 7, new DbChangeSet([], "ContextA", 7, DateTimeOffset.UtcNow)); // buffered

        // Act
        _tracker.Reset();

        // Assert
        _tracker.GetLastApplied("ContextA").Should().Be(0);
        _tracker.GetLastApplied("ContextB").Should().Be(0);
        _tracker.GetBufferedCount("ContextA").Should().Be(0);
    }

    [Fact]
    public void Reset_specific_context_should_only_clear_that_context()
    {
        // Arrange
        _tracker.Submit("ContextA", 5, new DbChangeSet([], "ContextA", 5, DateTimeOffset.UtcNow));
        _tracker.Submit("ContextB", 10, new DbChangeSet([], "ContextB", 10, DateTimeOffset.UtcNow));

        // Act
        _tracker.Reset("ContextA");

        // Assert
        _tracker.GetLastApplied("ContextA").Should().Be(0);
        _tracker.GetLastApplied("ContextB").Should().Be(10);
    }

    [Fact]
    public void Submit_after_reset_should_accept_any_sequence()
    {
        // Arrange
        _tracker.Submit("TestContext", 5, new DbChangeSet([], "TestContext", 5, DateTimeOffset.UtcNow));
        _tracker.Reset();

        // Act — master's sequence counter continued
        var msg = new DbChangeSet([], "TestContext", 100, DateTimeOffset.UtcNow);
        var result = _tracker.Submit("TestContext", 100, msg);

        // Assert
        result.MessagesToApply.Should().HaveCount(1);
        result.RequiresFullSync.Should().BeFalse();
        _tracker.GetLastApplied("TestContext").Should().Be(100);
    }

    [Fact]
    public void Submit_should_reorder_out_of_order_delivery()
    {
        // Arrange — simulate concurrent master transactions delivered out of order
        _tracker.Submit("TestContext", 1, new DbChangeSet([], "TestContext", 1, DateTimeOffset.UtcNow));

        // Act — receive 3, then 2
        var msg3 = new DbChangeSet([], "TestContext", 3, DateTimeOffset.UtcNow);
        var result3 = _tracker.Submit("TestContext", 3, msg3);
        result3.WasBuffered.Should().BeTrue();

        var msg2 = new DbChangeSet([], "TestContext", 2, DateTimeOffset.UtcNow);
        var result2 = _tracker.Submit("TestContext", 2, msg2);

        // Assert — msg2 applied + msg3 drained
        result2.MessagesToApply.Should().HaveCount(2);
        result2.MessagesToApply[0].Should().BeSameAs(msg2);
        result2.MessagesToApply[1].Should().BeSameAs(msg3);
        _tracker.GetLastApplied("TestContext").Should().Be(3);
        _tracker.GetBufferedCount("TestContext").Should().Be(0);
    }

    [Fact]
    public void GetAllLastApplied_should_return_empty_when_no_contexts()
    {
        // Act
        var result = _tracker.GetAllLastApplied();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAllLastApplied_should_return_all_tracked_contexts()
    {
        // Arrange
        _tracker.Submit("ContextA", 5, new DbChangeSet([], "ContextA", 5, DateTimeOffset.UtcNow));
        _tracker.Submit("ContextB", 10, new DbChangeSet([], "ContextB", 10, DateTimeOffset.UtcNow));

        // Act
        var result = _tracker.GetAllLastApplied();

        // Assert
        result.Should().HaveCount(2);
        result["ContextA"].Should().Be(5);
        result["ContextB"].Should().Be(10);
    }

    [Fact]
    public void GetAllLastApplied_should_be_empty_after_reset()
    {
        // Arrange
        _tracker.Submit("ContextA", 1, new DbChangeSet([], "ContextA", 1, DateTimeOffset.UtcNow));
        _tracker.Reset();

        // Act
        var result = _tracker.GetAllLastApplied();

        // Assert
        result.Should().BeEmpty();
    }
}
