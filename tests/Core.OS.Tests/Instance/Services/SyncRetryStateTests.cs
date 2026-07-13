using AwesomeAssertions;
using Core.OS.Instance.Services;
using Xunit;

namespace Core.OS.Tests.Instance.Services;

public sealed class SyncRetryStateTests
{
    [Fact]
    public void Should_return_true_when_retries_available_on_record_failure()
    {
        // Arrange
        var state = new SyncRetryState();

        // Act
        var canRetry = state.RecordFailure();

        // Assert
        canRetry.Should().BeTrue();
        state.AttemptCount.Should().Be(1);
        state.IsDegraded.Should().BeFalse();
    }

    [Fact]
    public void Should_return_false_when_retries_exhausted_on_record_failure()
    {
        // Arrange
        var state = new SyncRetryState();
        state.RecordFailure(); // 1
        state.RecordFailure(); // 2

        // Act
        var canRetry = state.RecordFailure(); // 3 — exhausted

        // Assert
        canRetry.Should().BeFalse();
        state.AttemptCount.Should().Be(3);
        state.IsDegraded.Should().BeTrue();
    }

    [Fact]
    public void Should_clear_attempt_count_and_degraded_flag_on_reset()
    {
        // Arrange
        var state = new SyncRetryState();
        state.RecordFailure();
        state.RecordFailure();
        state.RecordFailure();

        // Act
        state.Reset();

        // Assert
        state.AttemptCount.Should().Be(0);
        state.IsDegraded.Should().BeFalse();
    }

    [Fact]
    public void Should_respect_custom_max_retries_on_record_failure()
    {
        // Arrange
        var state = new SyncRetryState { MaxRetries = 1 };

        // Act
        var canRetry = state.RecordFailure();

        // Assert
        canRetry.Should().BeFalse();
        state.IsDegraded.Should().BeTrue();
    }
}
