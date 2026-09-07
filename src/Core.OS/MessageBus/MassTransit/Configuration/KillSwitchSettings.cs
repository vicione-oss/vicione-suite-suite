using System.ComponentModel.DataAnnotations;

namespace Core.OS.MessageBus.MassTransit.Configuration;

/// <summary>
/// Settings of the kill switch that stops a receive endpoint while a share of its messages keeps faulting.
/// See ADR-004 (D4).
/// </summary>
public sealed class KillSwitchSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Number of messages that must have been consumed within <see cref="TrackingPeriodInSeconds"/> before the kill
    /// switch arms itself. Counted per message, not per delivery attempt: the retry filter runs inside the kill
    /// switch's consume observer, so a message retried four times still counts once.
    /// The MassTransit default of 100 is unreachable on a low traffic edge endpoint.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int ActivationThreshold { get; set; } = 5;

    /// <summary>
    /// Percentage of failed messages within the tracking period that stops the endpoint.
    /// </summary>
    /// <remarks>
    /// The range starts at 1, not 0. A threshold of <c>0</c> is satisfied by any failure rate at all, so once the
    /// endpoint is armed the very next failure stops it for <see cref="RestartTimeoutInSeconds"/> — measured: with a
    /// single failing message among eight, an endpoint at <c>0</c> stopped after three while the same endpoint at
    /// <c>50</c> consumed all eight. A node with one transient failure an hour would park the endpoint every time.
    /// <c>1</c> already expresses "stop on the first failure once armed", so <c>0</c> only adds the accident — and it
    /// is an easy one to have, because <see cref="ErrorQueueSettings"/> uses <c>0</c> to mean "omit this bound".
    /// Turning the switch off is <see cref="Enabled"/>.
    /// </remarks>
    [Range(1, 100)]
    public int TripThresholdPercent { get; set; } = 50;

    /// <summary>
    /// Window over which messages and failures are counted. Long enough that an idle edge endpoint still reaches
    /// <see cref="ActivationThreshold"/>: a quiet node's instance queue sees roughly one message per minute, because
    /// health information is published on the health check period (60 s by default).
    /// </summary>
    [Range(1, 3600)]
    public int TrackingPeriodInSeconds { get; set; } = 300;

    /// <summary>
    /// Time the endpoint stays stopped before it restarts itself.
    /// </summary>
    [Range(1, 3600)]
    public int RestartTimeoutInSeconds { get; set; } = 60;
}
