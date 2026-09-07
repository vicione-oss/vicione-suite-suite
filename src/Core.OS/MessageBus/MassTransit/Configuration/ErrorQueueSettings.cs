using System.ComponentModel.DataAnnotations;

namespace Core.OS.MessageBus.MassTransit.Configuration;

/// <summary>
/// Settings applied to the lazily created <c>_error</c> and <c>_skipped</c> queues. Without them a master keeps its
/// fault history forever. See ADR-004 (D5). A value of <c>0</c> omits the corresponding queue argument, which is also
/// the escape hatch for a broker that already declared those queues with different arguments.
/// </summary>
public sealed class ErrorQueueSettings
{
    /// <summary>
    /// Time a faulted message is kept and time after which an idle error queue deletes itself.
    /// </summary>
    [Range(0, 365)]
    public int RetentionInDays { get; set; } = 7;

    [Range(0, int.MaxValue)]
    public int MaxMessages { get; set; } = 250;

    [Range(0, 1024)]
    public int MaxSizeInMegabytes { get; set; } = 2;
}
