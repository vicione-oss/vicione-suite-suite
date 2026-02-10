using Core.OS.Monitoring;
using Core.Shared.Monitoring;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Journal;
using ViciOne.Journal;
using Xunit;

namespace Core.OS.Tests.Monitoring;

public class JournalFieldsCollector_CollectMetrics
{
    [Fact]
    public async Task Returns_one_minute_interval_values()
    {
        var options = Substitute.For<IOptions<JournalFieldsCollectorOptions>>();
        var journalMonitoring = Substitute.For<IJournalMonitoring>();
        journalMonitoring.FilterEntries.Returns(new Dictionary<string, JournalFilterEntry>
        {
            { "MQTT", new("MQTT", "SYSLOG_IDENTIFIER=mosquitto", 0) },
            { "Suite", new("Suite", "SYSLOG_IDENTIFIER=Suite", 1) }
        });
        options.Value.Returns(new JournalFieldsCollectorOptions
        {
            Fields = ["UNIT", "PRIORITY", "_PID", "SYSLOG_IDENTIFIER"],
            StringComparer = StringComparer.Ordinal,
        });
        var session = Substitute.For<IJournalGenericSession>();
        var lastFilter = string.Empty;
        session.SetFilter(Arg.Do<string>(a => lastFilter = a));
        session.GetLogs(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(_ => lastFilter switch
            {
                "SYSLOG_IDENTIFIER=mosquitto" => [
                    new Dictionary<string, string>
                    {
                        { "UNIT", "mosquitto" },
                        { "PRIORITY", "3" },
                        { "_PID", "1234" },
                        { "SYSLOG_IDENTIFIER", "mosquitto" },
                    }
                ],
                "SYSLOG_IDENTIFIER=Suite" => [
                    new Dictionary<string, string>
                    {
                        { "UNIT", "Suite" },
                        { "PRIORITY", "3" },
                        { "_PID", "5678" },
                        { "SYSLOG_IDENTIFIER", "Suite" },
                    }
                ],
                _ => []
            });
        JournalFieldsCollector collector = new(options, journalMonitoring, _ => Task.FromResult(session));

        var metrics = await collector.CollectMetrics(1, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        metrics.Should().HaveCount(2).And.SatisfyRespectively(
            m =>
            {
                m.Name.Should().Be(JournalFieldsMetric.Count);
                m.SubGroup.Should().Be("MQTT");
                m.Value.Should().BeOfType<Dictionary<string, int>>()
                    .And.Subject.Should().BeEquivalentTo(new Dictionary<string, int>
                    {
                        { "UNIT=mosquitto", 1 },
                        { "PRIORITY=3", 1 },
                        { "_PID=1234", 1 },
                        { "SYSLOG_IDENTIFIER=mosquitto", 1 },
                    });
            },
            m =>
            {
                m.Name.Should().Be(JournalFieldsMetric.Count);
                m.SubGroup.Should().Be("Suite");
                m.Value.Should().BeOfType<Dictionary<string, int>>()
                    .And.Subject.Should().BeEquivalentTo(new Dictionary<string, int>
                    {
                        { "UNIT=Suite", 1 },
                        { "PRIORITY=3", 1 },
                        { "_PID=5678", 1 },
                        { "SYSLOG_IDENTIFIER=Suite", 1 },
                    });
            });
    }

    [Fact]
    public async Task Returns_fifteen_minute_interval_values()
    {
        var options = Substitute.For<IOptions<JournalFieldsCollectorOptions>>();
        var journalMonitoring = Substitute.For<IJournalMonitoring>();
        journalMonitoring.FilterEntries.Returns(new Dictionary<string, JournalFilterEntry>
        {
            { "MQTT", new("MQTT", "SYSLOG_IDENTIFIER=mosquitto", 0) },
            { "Suite", new("Suite", "SYSLOG_IDENTIFIER=Suite", 1) }
        });
        options.Value.Returns(new JournalFieldsCollectorOptions
        {
            Fields = ["UNIT", "PRIORITY", "_PID", "SYSLOG_IDENTIFIER"],
            StringComparer = StringComparer.Ordinal,
        });
        var session = Substitute.For<IJournalGenericSession>();
        var lastFilter = string.Empty;
        session.SetFilter(Arg.Do<string>(a => lastFilter = a));
        session.GetLogs(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(_ => lastFilter switch
        {
            "SYSLOG_IDENTIFIER=mosquitto" => [
                new Dictionary<string, string>
                    {
                        { "UNIT", "mosquitto" },
                        { "PRIORITY", "3" },
                        { "_PID", "1234" },
                        { "SYSLOG_IDENTIFIER", "mosquitto" },
                    }
            ],
            "SYSLOG_IDENTIFIER=Suite" => [
                new Dictionary<string, string>
                    {
                        { "UNIT", "Suite" },
                        { "PRIORITY", "3" },
                        { "_PID", "5678" },
                        { "SYSLOG_IDENTIFIER", "Suite" },
                    }
            ],
            _ => []
        });
        JournalFieldsCollector collector = new(options, journalMonitoring, _ => Task.FromResult(session));

        await collector.CollectMetrics(1, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        var metrics = await collector.CollectMetrics(15, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        metrics.Should().HaveCount(4).And.SatisfyRespectively(
            m => m.IntervalInMinutes.Should().Be(1),
            m =>
            {
                m.Name.Should().Be(JournalFieldsMetric.Count);
                m.SubGroup.Should().Be("MQTT");
                m.IntervalInMinutes.Should().Be(15);
                m.Value.Should().BeOfType<Dictionary<string, int>>()
                    .And.Subject.Should().BeEquivalentTo(new Dictionary<string, int>
                    {
                        { "UNIT=mosquitto", 2 },
                        { "PRIORITY=3", 2 },
                        { "_PID=1234", 2 },
                        { "SYSLOG_IDENTIFIER=mosquitto", 2 },
                    });
            },
            m => m.IntervalInMinutes.Should().Be(1),
            m =>
            {
                m.Name.Should().Be(JournalFieldsMetric.Count);
                m.SubGroup.Should().Be("Suite");
                m.IntervalInMinutes.Should().Be(15);
                m.Value.Should().BeOfType<Dictionary<string, int>>()
                    .And.Subject.Should().BeEquivalentTo(new Dictionary<string, int>
                    {
                        { "UNIT=Suite", 2 },
                        { "PRIORITY=3", 2 },
                        { "_PID=5678", 2 },
                        { "SYSLOG_IDENTIFIER=Suite", 2 },
                    });
            });
    }

    [Fact]
    public async Task Filters_fields()
    {
        var options = Substitute.For<IOptions<JournalFieldsCollectorOptions>>();
        var journalMonitoring = Substitute.For<IJournalMonitoring>();
        journalMonitoring.FilterEntries.Returns(new Dictionary<string, JournalFilterEntry>
        {
            { "Suite", new("Suite", "SYSLOG_IDENTIFIER=*", 1) }
        });
        options.Value.Returns(new JournalFieldsCollectorOptions
        {
            Fields = ["_PID"],
        });
        var session = Substitute.For<IJournalGenericSession>();
        session.GetLogs(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(
        [
            new Dictionary<string, string>
            {
                { "_PID", "1234" },
                { "SYSLOG_IDENTIFIER", "mosquitto" },
            },
            new Dictionary<string, string>
            {
                { "_PID", "5678" },
                { "SYSLOG_IDENTIFIER", "Suite" },
            }
        ]);
        JournalFieldsCollector collector = new(options, journalMonitoring, _ => Task.FromResult(session));

        var metrics = await collector.CollectMetrics(1, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        metrics.Should().ContainSingle().Which.Value.Should().BeOfType<Dictionary<string, int>>().Which.Count.Should().Be(2);
    }
}
