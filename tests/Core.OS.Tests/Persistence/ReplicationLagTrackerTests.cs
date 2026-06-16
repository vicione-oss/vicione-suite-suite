using AwesomeAssertions;
using Core.OS.Persistence;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Core.OS.Tests.Persistence;

public sealed class ReplicationLagTrackerTests
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly ReplicationLagTracker _tracker;

    public ReplicationLagTrackerTests()
    {
        _timeProvider.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        _tracker = new ReplicationLagTracker(_timeProvider);
    }

    [Fact]
    public void Record_should_store_lag_per_context_type()
    {
        // Arrange
        var publishedAt = _timeProvider.GetUtcNow() - TimeSpan.FromSeconds(5);

        // Act
        _tracker.Record("ContextA", publishedAt);

        // Assert
        var lag = _tracker.GetLag("ContextA");
        lag.Should().NotBeNull();
        lag!.Value.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GetLag_should_return_null_for_unknown_context()
    {
        // Arrange / Act
        var lag = _tracker.GetLag("UnknownContext");

        // Assert
        lag.Should().BeNull();
    }

    [Fact]
    public void Record_should_clamp_negative_lag_to_zero()
    {
        // Arrange — publishedAt is in the future (simulates clock skew)
        var publishedAt = _timeProvider.GetUtcNow() + TimeSpan.FromMinutes(1);

        // Act
        _tracker.Record("ContextA", publishedAt);

        // Assert
        var lag = _tracker.GetLag("ContextA");
        lag.Should().NotBeNull();
        lag!.Value.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Record_should_update_lag_on_subsequent_calls()
    {
        // Arrange
        _tracker.Record("ContextA", _timeProvider.GetUtcNow() - TimeSpan.FromMinutes(10));

        // Act — more recent message
        _tracker.Record("ContextA", _timeProvider.GetUtcNow() - TimeSpan.FromSeconds(2));

        // Assert — lag should now reflect the most recent message
        var lag = _tracker.GetLag("ContextA");
        lag.Should().NotBeNull();
        lag!.Value.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void GetAllLags_should_return_all_tracked_contexts()
    {
        // Arrange
        _tracker.Record("ContextA", _timeProvider.GetUtcNow() - TimeSpan.FromSeconds(1));
        _tracker.Record("ContextB", _timeProvider.GetUtcNow() - TimeSpan.FromSeconds(2));

        // Act
        var lags = _tracker.GetAllLags();

        // Assert
        lags.Should().HaveCount(2);
        lags.Should().ContainKey("ContextA");
        lags.Should().ContainKey("ContextB");
    }
}
