# OpenTelemetry

The suite exports **traces**, **metrics**, and **logs** over OTLP.
Traces and metrics come from the OpenTelemetry SDK pipeline (`AddSuiteOpenTelemetry`).
Logs are exported through a Serilog sink (`Serilog.Sinks.OpenTelemetry`) that runs alongside the other log targets (Console, LogFile, Journal).

For the general OTLP environment variables, see the [OpenTelemetry SDK configuration reference](https://opentelemetry.io/docs/specs/otel/configuration/sdk-environment-variables/).
This document only covers the suite-specific behaviour and the specialities that matter for edge devices.

## Enablement

OpenTelemetry is **off by default** on every target platform.
It is gated twice, and both gates must be open before anything is exported:

1. An OTLP endpoint must be configured via `OTEL_EXPORTER_OTLP_ENDPOINT`.
   Without it the whole OTel pipeline (traces, metrics, and logs) stays disabled.
2. For **logs** specifically, `OpenTelemetry` must additionally be listed in `Logging:LogTargets`.

The shipped configuration lists only `Journal` in `Logging:LogTargets` and sets no endpoint, so the sink is dormant: no background worker, no queue, no socket.
Enable it per device through `/etc/vicione-suite/conf.d/*.conf` (see [edge-s.md](./edge-s.md) for how environment variables are layered).

`Logging:LogTargets` is a positional array, so the OTLP target has to be *added* at a free index.
Overriding index 0 replaces the target the device already writes to instead of adding one — check what it is with `cat /etc/vicione-suite/conf.d/*` before editing.
Dropping `Journal` that way also drops the log throttling, because the spam guard is applied by the journal sink only.

Example `conf.d` snippet to enable log export on a single device:

```text
Logging__LogTargets__0='Journal'
Logging__LogTargets__1='OpenTelemetry'
OTEL_EXPORTER_OTLP_ENDPOINT='http://collector.example:4317'
```

## Configuration

Log export is tuned through the `Logging:OpenTelemetry` section (env-var form `Logging__OpenTelemetry__*`).

| Key                                  | Default       | Purpose                                                                        |
|--------------------------------------|---------------|--------------------------------------------------------------------------------|
| `Logging:OpenTelemetry:MinimumLevel` | `Information` | Floor for events exported via OTLP. Independent of `Logging:LogLevel`.         |
| `Logging:OpenTelemetry:QueueLimit`   | `10000`       | Maximum number of events buffered in memory while the endpoint is unreachable. |

Resource identity (`service.name`, `service.namespace`, `service.instance.id`, `service.version`) is shared between the traces/metrics pipeline and the log sink, so logs and traces correlate under the same service and instance in the backend.
`OTEL_SERVICE_NAME` overrides `service.name`; the instance id is read from the device's local instance id file.

Standard `OTEL_EXPORTER_OTLP_*` environment variables take precedence over values from other configuration providers, including instance configuration distributed by a master.

## Edge-device specialities

### Off by default is deliberate

The double gate exists so that a stock edge device carries the code but pays no runtime cost.
Turning it on is an explicit, per-device decision — never a side effect of a suite update.

### Memory: `QueueLimit`

When the collector is unreachable — a normal condition for edge devices with intermittent connectivity — events accumulate in memory up to `QueueLimit`, then the oldest are dropped.
The default of `10000` is tuned for mainstream (>= 4 GB) devices.
On the low-power edgeGateway **Size S** (1 GB RAM, "very low power"; see [target-platforms.md](./target-platforms.md)) lower it, e.g. `Logging__OpenTelemetry__QueueLimit='1000'`, so a long disconnection cannot grow the buffer into a meaningful fraction of RAM.
A value below 1 is clamped to 1 rather than rejected — a typo in this knob must not keep an unattended device from starting — and the fallback is reported as a startup warning.

### Export level floor

`MinimumLevel` caps the **level** of exported events, not their **rate**.
Its purpose is to stop a runtime log-level change (e.g. enabling `Debug` for local troubleshooting) from streaming verbose logs over a potentially metered uplink.
A component that is chatty at `Information` can still produce significant egress — control that with per-source log-level overrides, not with this setting.

### Transport: gRPC vs. http/protobuf

The default transport is gRPC on port `4317`, which requires HTTP/2 end to end.
On locked-down industrial networks HTTP/2 to `4317` may be blocked where `http/protobuf` on `4318` is allowed.
Select the transport explicitly with `OTEL_EXPORTER_OTLP_PROTOCOL` (`grpc` or `http/protobuf`); any other value falls back to gRPC and is reported as a startup warning.

### Fail-safe on misconfiguration

A misconfigured endpoint, protocol, or a sink that rejects malformed OTLP variables must never crash-loop an unattended device.
Such problems degrade to a startup **warning** and the suite continues with its remaining log targets.
The warnings are visible in the journal (`sudo journalctl -u vicione-suite`).

### Shutdown flush

On abort and crash paths the static logger is flushed explicitly (`Log.CloseAndFlushAsync`) so the batched OTLP events — including the final crash log — are delivered before the process exits.
If the collector is unreachable this flush waits for the export timeout, so process exit is not instant on a crash.
Do not configure an aggressive systemd stop timeout that would `SIGKILL` the process mid-flush.

## Storage footprint

Enabling log export adds no meaningful storage cost.
The sink assembly reuses `Google.Protobuf` and `Grpc.Net.Client`, which the traces/metrics OTLP exporter already ships.
This matters on the storage-constrained Size S (1.7 GB including OS).
