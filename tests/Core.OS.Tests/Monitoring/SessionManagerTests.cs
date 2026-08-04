using Core.OS.Monitoring;
using ViciOne.Journal;

namespace Core.OS.Tests.Monitoring;

public class SessionManagerTests
{
    public sealed class Process
    {
        [Fact]
        public async Task Should_accumulate_counts_for_configured_fields_and_set_last_cursor()
        {
            // Arrange
            var fieldSet = new[] { "level", "status" };
            var session = Substitute.For<IJournalGenericSession>();
            session.GetLogs(Arg.Any<int>(), true, false).Returns(
            [
                new Dictionary<string,string> { ["level"]="info", ["status"]="ok", ["ignored"]="x" },
                new Dictionary<string,string> { ["status"]="ok" }
            ]);
            session.GetCursor().Returns("CURSOR123");

            var sut = new SessionManager(
                _ => Task.FromResult(session),
                "filter123",
                fieldSet,
                StringComparer.OrdinalIgnoreCase);

            // Act
            await sut.Process(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

            // Assert
            var currentCounts = sut.GetCurrentCounts();
            currentCounts.Keys.Should().BeEquivalentTo(
            [
                "level=info","status=ok"
            ]);
            currentCounts["status=ok"].Should().Be(2);

            var bufferCounts = sut.GetCountsBuffer();
            bufferCounts["status=ok"].Should().Be(2);
            bufferCounts["level=info"].Should().Be(1);

            session.Received().SetFilter("filter123");
            session.Received().SeekRealTime(Arg.Any<DateTime>());
        }

        [Fact]
        public async Task Should_use_seekcursor_if_lastcursor_exists()
        {
            // Arrange
            var session = Substitute.For<IJournalGenericSession>();
            session.GetLogs(Arg.Any<int>(), true, false)
                .Returns([new Dictionary<string, string> { ["level"] = "info" }]);
            session.GetCursor().Returns("C1");

            var sut = new SessionManager(
                _ => Task.FromResult(session),
                "f",
                ["level"],
                StringComparer.Ordinal);

            // call once to _lastCursor set
            await sut.Process(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

            // Act
            await sut.Process(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

            // Assert
            session.Received().SeekCursor("C1");
        }

        [Fact]
        public async Task Should_not_read_the_last_entry_by_cursor_again()
        {
            // Arrange
            var session = Substitute.For<IJournalGenericSession>();
            session.GetLogs(Arg.Any<int>(), Arg.Any<bool>(), true)
                .Returns([
                    new Dictionary<string, string> { ["level"] = "info" },
                ]);
            session.GetLogs(Arg.Any<int>(), Arg.Any<bool>(), false)
                .Returns([
                    new Dictionary<string, string> { ["level"] = "info" },
                    new Dictionary<string, string> { ["level"] = "info" },
                ]);
            session.GetCursor().Returns("C1");

            var sut = new SessionManager(
                _ => Task.FromResult(session),
                "f",
                ["level"],
                StringComparer.Ordinal);

            // call once to _lastCursor set
            await sut.Process(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
            sut.GetCurrentCounts();

            // Act
            await sut.Process(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

            // Assert
            sut.GetCurrentCounts().Should().ContainSingle().Which.Value.Should().Be(1);
        }
    }

    public sealed class GetCurrentCounts
    {
        [Fact]
        public void Should_return_and_clear_current_counts()
        {
            // Arrange
            var sut = new SessionManager(_ => null!, "f", ["f1"], StringComparer.Ordinal);
            var countsField = typeof(SessionManager).GetField("_currentCounts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var dict = (Dictionary<string, int>)countsField.GetValue(sut)!;
            dict["a=1"] = 5;

            // Act
            var result = sut.GetCurrentCounts();

            // Assert
            result.Should().ContainKey("a=1");
            result["a=1"].Should().Be(5);
            dict.Should().BeEmpty();
        }
    }

    public sealed class GetCountsBuffer
    {
        [Fact]
        public void Should_return_and_clear_buffer_counts()
        {
            // Arrange
            var sut = new SessionManager(_ => null!, "f", ["f1"], StringComparer.Ordinal);
            var countsField = typeof(SessionManager).GetField("_countsBuffer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var dict = (Dictionary<string, int>)countsField.GetValue(sut)!;
            dict["x=9"] = 9;

            // Act
            var result = sut.GetCountsBuffer();

            // Assert
            result.Should().ContainKey("x=9");
            result["x=9"].Should().Be(9);
            dict.Should().BeEmpty();
        }
    }
}
