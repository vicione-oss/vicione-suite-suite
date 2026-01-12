using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using Blazor.Shared.Logging.Contracts;

namespace Blazor.Shared.Logging;

internal partial class JournalEntryParser
{
    internal static bool TryParseEntry(IReadOnlyDictionary<string, string> data, [NotNullWhen(true)] out JournalEntry? entry)
    {
        /// Journal fields could be retrieved from journal service. Default field names can be find here.
        /// <see href="https://github.com/systemd/systemd/blob/1f5d8a6132f12574f524cef72dbda0f7408c4217/man/systemd.journal-fields.xml"/>
        if (data.TryGetValue("REALTIME_TIMESTAMP", out var timestamp) &&
            data.TryGetValue("_HOSTNAME", out var hostname) &&
            data.TryGetValue("SYSLOG_IDENTIFIER", out var syslogId) &&
            data.TryGetValue("PRIORITY", out var priority) &&
            data.TryGetValue("MESSAGE", out var message))
        {
            entry = new JournalEntry(
                DateTimeOffset.Parse(timestamp, CultureInfo.InvariantCulture),
                hostname,
                syslogId,
                data.TryGetValue("_PID", out var pid) ? int.Parse(pid, CultureInfo.InvariantCulture) : null,
                ParsePriority(priority),
                message);
            return true;
        }
        entry = default;
        return false;
    }

    private static int ParsePriority(string priority)
    {
        if (!PriorityRegex().IsMatch(priority))
            return 6;

        return int.Parse(priority, CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"[0-7]")]
    private static partial Regex PriorityRegex();
}
