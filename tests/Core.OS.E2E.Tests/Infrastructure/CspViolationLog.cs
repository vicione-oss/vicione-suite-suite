using System.Collections.Concurrent;
using Microsoft.Playwright;

namespace Core.OS.E2E.Tests.Infrastructure;

/// <summary>
/// Collects the Content Security Policy refusals the browser writes to the console while a test
/// drives the UI, across navigations. Under an enforcing policy a blocked resource is otherwise
/// silent: the page still renders, the feature is dead, and no assertion notices.
/// </summary>
public sealed class CspViolationLog
{
    /// <summary>
    /// The phrase a refusal message carries. It names the directive that blocked the resource, and
    /// nothing else in the console mentions the policy, so this identifies refusals without
    /// matching unrelated errors.
    /// </summary>
    public const string RefusalPhrase = "Content Security Policy";

    private readonly ConcurrentQueue<string> _refusals = new();

    /// <summary>Everything the browser refused since <see cref="Watch"/>, as it reported it.</summary>
    public IReadOnlyCollection<string> Refusals => [.. _refusals];

    /// <summary>Starts recording on <paramref name="page"/>; attach before the first navigation.</summary>
    public static CspViolationLog Watch(IPage page)
    {
        var log = new CspViolationLog();

        page.Console += (_, message) => log.Record(message);

        return log;
    }

    /// <summary>True when <paramref name="message"/> reports a resource the policy blocked.</summary>
    public static bool IsRefusal(IConsoleMessage message)
        => message.Text.Contains(RefusalPhrase, StringComparison.OrdinalIgnoreCase);

    private void Record(IConsoleMessage message)
    {
        if (IsRefusal(message))
            _refusals.Enqueue(message.Text);
    }
}
