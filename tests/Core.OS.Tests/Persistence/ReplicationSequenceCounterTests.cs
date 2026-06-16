using Core.OS.Persistence;
using AwesomeAssertions;
using Xunit;

namespace Core.OS.Tests.Persistence;

public class ReplicationSequenceCounterTests
{
    private readonly ReplicationSequenceCounter _counter = new();

    [Fact]
    public void Next_should_start_at_one()
    {
        // Act
        var result = _counter.Next("TestContext");

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public void Next_should_increment_monotonically()
    {
        // Act
        var first = _counter.Next("TestContext");
        var second = _counter.Next("TestContext");
        var third = _counter.Next("TestContext");

        // Assert
        first.Should().Be(1);
        second.Should().Be(2);
        third.Should().Be(3);
    }

    [Fact]
    public void Next_should_track_contexts_independently()
    {
        // Act
        var a1 = _counter.Next("ContextA");
        var b1 = _counter.Next("ContextB");
        var a2 = _counter.Next("ContextA");
        var b2 = _counter.Next("ContextB");

        // Assert
        a1.Should().Be(1);
        a2.Should().Be(2);
        b1.Should().Be(1);
        b2.Should().Be(2);
    }

    [Fact]
    public void Current_should_return_zero_for_unknown_context()
    {
        // Act
        var result = _counter.Current("Unknown");

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void Current_should_return_last_assigned_value()
    {
        // Arrange
        _counter.Next("TestContext");
        _counter.Next("TestContext");
        _counter.Next("TestContext");

        // Act
        var result = _counter.Current("TestContext");

        // Assert
        result.Should().Be(3);
    }

    [Fact]
    public void Seed_should_set_initial_value()
    {
        // Act
        _counter.Seed("TestContext", 100);

        // Assert
        _counter.Current("TestContext").Should().Be(100);
    }

    [Fact]
    public void Seed_should_not_regress_counter()
    {
        // Arrange
        _counter.Seed("TestContext", 100);

        // Act
        _counter.Seed("TestContext", 50);

        // Assert
        _counter.Current("TestContext").Should().Be(100);
    }

    [Fact]
    public void Next_after_seed_should_continue_from_seeded_value()
    {
        // Arrange
        _counter.Seed("TestContext", 100);

        // Act
        var result = _counter.Next("TestContext");

        // Assert
        result.Should().Be(101);
    }

    [Fact]
    public void GetAll_should_return_all_contexts()
    {
        // Arrange
        _counter.Next("ContextA");
        _counter.Next("ContextA");
        _counter.Next("ContextB");

        // Act
        var all = _counter.GetAll();

        // Assert
        all.Should().HaveCount(2);
        all["ContextA"].Should().Be(2);
        all["ContextB"].Should().Be(1);
    }

    [Fact]
    public void GetAll_should_return_empty_when_no_contexts()
    {
        // Act
        var all = _counter.GetAll();

        // Assert
        all.Should().BeEmpty();
    }

    [Fact]
    public void GetAll_should_include_seeded_contexts()
    {
        // Arrange
        _counter.Seed("SeededContext", 42);

        // Act
        var all = _counter.GetAll();

        // Assert
        all.Should().ContainKey("SeededContext");
        all["SeededContext"].Should().Be(42);
    }
}
