# ADR-004: Error Handling Strategy

## Status

Accepted

## Date

2026-08-03

## Context

Phase 3a of the architecture review established that the Suite has *a* retry policy but not *an error handling strategy*. The pipeline is configured in exactly one place — `Core.OS/MessageBus/MassTransit/Configuration/MassTransitConfiguration.cs` — and today it provides:

- `cfg.UseMessageRetry(r => r.Intervals(100, 500, 1000, 2000))` — **only on the RabbitMQ bus**
- `cfg.UseInMemoryOutbox(context)` — only on the RabbitMQ bus
- `AddEntityFrameworkOutbox` + `UseBusOutbox` — master only (PostgreSQL)
- `ConcurrentMessageLimit = 1` on Standalone, `PrefetchCount = 1` on `DbChangeSetConsumer`
- Durable queues; `QueueExpiration = 5 days` on every non-master node

and it provides **no** circuit breaker, **no** rate limiter, **no** delayed or scheduled redelivery, **no** kill switch, **no** explicit error-queue configuration and **no** `IConsumer<Fault<T>>` anywhere in the repository.

Two consequences of that were left open by the audit and are resolved here.

Throughout, "the bus" means the instance's main bus. A slave also runs a second, in-memory `ILocalBus` for its own requests; D1 records why it is out of scope.

### Finding 1 — the in-memory bus has no retry at all

`UseMessageRetry` is called inside `UsingRabbitMq(...)`. The `UsingInMemory(...)` branch calls only `Configure(...)` and `ConfigureEndpoints(...)`.

The in-memory bus is not a test-only convenience. `build/package_settings.sh` sets `MessageBus__UseInMemoryBus='true'` for both the `STANDALONE` and the `MINIMAL` packaging profiles, i.e. **the Edge-S product runs the in-memory transport**. Every deployed edge device therefore consumes messages with a single attempt and no recovery: one transient SQLite lock, one momentary module-loading race, and the message is gone.

| Profile (`build/package_settings.sh`) | Instance type | Transport |
|---|---|---|
| `STANDALONE` | Standalone | in-memory |
| `MINIMAL` (Debian base package) | Standalone | in-memory |
| `MASTER`, `REVIEW` | Master | RabbitMQ on `localhost` |
| `SLAVE` | Slave | RabbitMQ on the master's host |

### Finding 2 — dead-letter behaviour is opposite on the two transports

Verified against MassTransit 8.5.10 (the pinned `<MassTransitVersion>`).

**RabbitMQ.** After the retry filter gives up, `ErrorTransportFilter` hands the raw message to the endpoint's `IErrorTransport`. `RabbitMqReceiveEndpointBuilder.CreateErrorTransport()` builds it from `RabbitMqErrorSettings(settings, "<queue>_error")`; unhandled message types go the same way through `RabbitMqDeadLetterSettings` to `<queue>_skipped`. Both settings classes are constructed from the *input* endpoint's `ReceiveSettings` and copy `Durable`, `AutoDelete`, `ExchangeArguments` **and `QueueArguments`**.

That last detail decides the retention:

- `IRabbitMqQueueConfigurator.QueueExpiration` is **not** a standalone field — its getter/setter is backed by `QueueArguments[x-expires]`. Because `QueueArguments` is copied, the 5-day expiry that `AddConfigureEndpointsCallback` applies to non-master endpoints **is inherited by their `_error` and `_skipped` queues**. Slave fault queues self-clean.
- On a **master** the callback deliberately skips `QueueExpiration` (`instanceOptions.Type != InstanceType.Master`). Master `_error` and `_skipped` queues are therefore durable, have no `x-expires`, no `x-message-ttl`, no `x-max-length`, no consumer and no purge. **They grow without bound, forever.**
- The `raq.SetExchangeArgument("x-expires", …)` line next to it sets the argument on the *exchange*. RabbitMQ honours `x-expires` on queues only; the effective TTL comes from `raq.QueueExpiration`. The exchange argument is inert and is left untouched — changing an existing exchange's arguments would make the redeclare fail with `PRECONDITION_FAILED`.
- Error/skipped queues are declared **lazily**, on the first fault of that endpoint. A healthy deployment has none.

**In-memory.** `InMemoryReceiveTransport.Start()` creates `InMemoryMessageErrorTransport` / `InMemoryMessageDeadLetterTransport` pointing at *message-fabric exchanges* called `<queue>_error` / `<queue>_skipped`. `TransportInMemoryReceiveEndpointContext.ConfigureTopology()` declares and binds only the input exchange/queue pair; nothing is ever bound to those two exchanges, and `MessageFabric.GetOrAddExchange` happily creates a fan-out exchange with no sinks. A fan-out exchange with no sinks **discards**.

⇒ On Edge-S a faulted message is silently destroyed. There is no queue, no file, no log record of the payload — only whatever the consumer logged before it threw.

**Both transports** additionally publish a `Fault<T>` event (`PublishFaults` defaults to `true`). Nothing consumes it today, so on RabbitMQ the fault exchange has no bound queue and the broker drops the copy.

### Finding 3 — flash cost on Edge-S

| Node | Broker location | `_error` retention today | Flash cost today |
|---|---|---|---|
| Standalone (Edge-S) | none (in-memory) | none — discarded | 0 bytes, and 0 evidence |
| Slave | master's broker | inherits `x-expires` = 5 days | 0 bytes locally |
| Master | its own broker | **unbounded** | unbounded, on the master's disk |

The exposure is asymmetric and both halves are wrong: **the edge loses the evidence, the master keeps it forever.** Durable RabbitMQ messages are written to the message store and the queue index, so a permanently failing consumer or a client retry-loop writes to the broker's disk on every fault with no upper bound — the one failure mode a 2.3 GB flash device with limited write cycles cannot absorb.

### Finding 4 — retry and correlated feedback are mutually exclusive today

`cfg.UseInMemoryOutbox(context)` buffers everything a consumer publishes and flushes it only when the consumer returns successfully. A consumer that throws publishes nothing. Therefore the two things an operator-facing consumer wants — *retry the work* and *tell the caller it failed* — cannot both be done from inside the consumer. `EnqueueModulePackageOperationsConsumer` chose feedback and swallowed the exception, which costs it every retry.

### Finding 5 — there are no per-message-type endpoints; every queue is shared and serialized

`SuiteEndpointNameFormatter.GetConsumerName` does not derive an endpoint per message type. A message without a `MessageEndpointAttribute` falls back to a queue named after its *kind*: `MessagingHelper.CommandsQueueName`, `EventsQueueName` or `RequestsQueueName`. Instance-dependent consumers and `[ReadOnlyConsumer]`s all collapse onto `Instance_<instance-id>`. Measured against `src/Core.OS` (61 consumers; courier activities come from a separate scan and are not counted here):

| Endpoint | Consumers on it |
|---|---|
| `Commands` | 28 |
| `Requests` | 18 |
| `Instance_<instance-id>` | 11 — replication (`DbChangeSet`) plus every instance-dependent command and `[ReadOnlyConsumer]` |
| `Events` | 3 |
| `…EnqueueModulePackageOperationsFault` | 1 |

Only fault consumers, courier activities and messages that declare an explicit `MessageEndpointAttribute` get an endpoint of their own.

All of these are additionally serialized. On Standalone `ConcurrentMessageLimit = 1` is the per-receive-endpoint default, so each queue processes one message at a time. `DbChangeSetConsumerDefinition` pins `PrefetchCount = 1` on the instance queue and `CreateConnectionConsumerDefinition` pins it on `Commands`.

Three consequences follow. First, retry cannot be classified per message type at endpoint level — the endpoint is all the retry filter can see. Second, *any* retry on any of these queues blocks *every other message type* queued behind it: on the instance queue that includes replication, on `Commands` it includes the entire instance-independent command surface. Third — and this is what sizes D2 — no endpoint in the system today can afford a long ladder, so the ladder is chosen by the queue's serialization property rather than by the message class of a single consumer on it.

---

## Decision

### D1 — The pipeline is transport-independent

The retry ladder, the kill switch and the in-memory outbox are configured so that **Standalone (in-memory) and Master/Slave (RabbitMQ) behave identically**. Concretely, `UseMessageRetry` moves out of the RabbitMQ branch into the shared endpoint-configuration callback, which runs for both transports.

The in-memory outbox moves into the same callback, **inside** the retry filter, and applies on **every** transport. It is not there for broker atomicity — that is the EF Core Bus Outbox's job on the master — but to buffer what a consumer publishes and discard it when that consumer throws. Retry without it republishes the side effects of every failed attempt: a consumer that publishes an event and then fails would deliver that event once per attempt, so enabling retry on the in-memory transport without the outbox would replace "no recovery" with "duplicate events", precisely on the node type this decision exists to protect. Order is normative:

```
kill switch  →  retry  →  message scope  →  in-memory outbox  →  consumer
```

Retry must wrap both the message scope and the outbox, so that each attempt starts with fresh scoped dependencies *and* an empty outbox buffer. All three are added in that order in the same endpoint-configuration callback, and `MessageRetryPipelineTests.Should_discard_what_a_failed_attempt_published` and `Should_give_every_attempt_its_own_dependency_scope` pin the two halves.

**`UseMessageScope` must not be configured on the bus.** A bus-level consume filter wraps the endpoint-level ones, so a bus-level message scope sits *outside* the retry filter and every attempt of one message resolves from a single DI scope — measured: three attempts, one scope. That is the failure this decision most needs to avoid, because it is invisible: a consumer that failed inside `SaveChangesAsync` would be retried on the very same `DbContext` with its change tracker still holding the failed attempt's state, so it fails identically every time and the ladder achieves nothing. It is exactly the transient-database case D2's ladders are sized for. Before this ADR the ordering happened to be safe, because retry was also bus-level and registered ahead of `Configure(...)`; moving retry to the endpoint is what made the placement of the scope matter.

The scope sits *inside* retry and *outside* the outbox, so a scoped publish endpoint resolves to the buffered one.

**Scope: the main bus only.** A slave runs a *second* bus — `ILocalBus`, configured by `AddLocalBus` — an in-memory bus that answers the node's own instance-independent `IRequest` calls (`BackEndMediator.CreateRequest`) without a round trip to the master. It is deliberately left out of everything above, and that is not an oversight:

- Its only consumers are request consumers, which per D6a answer with an error response instead of throwing. A retry ladder would have nothing to retry and the kill switch, which arms on consumer faults, would never trip.
- Sharing the main bus's `AddConfigureEndpointsCallback` would be *wrong*, not merely redundant: that callback gates slave endpoints on `SynchronizationState`, and the local bus exists precisely so a slave can serve its own reads while it is still syncing.

Retry, in-memory outbox, kill switch and error-queue settings therefore describe the main bus. The local bus keeps only `UseMessageScope`. One known divergence is worth revisiting separately: it also skips `ConfigureJsonSerializerOptions`, so a locally answered request does not go through the same `DateTime` converter and null-handling as the same request answered by the master.

`ConcurrentMessageLimit = 1` on Standalone is unchanged by this ADR. Note what it actually is: the *per-receive-endpoint* default, not a global cap — a standalone node therefore already consumes one message per endpoint in parallel. It is kept because roughly 65 consumers across the repository were written under that serialization assumption without anything asserting it, because SQLite is configured with a bare `DataSource` (no WAL, no `busy_timeout`), and because several control-plane consumers do read-modify-write across a file-backed store. Raising it is a defensible change on a 2-core Cortex-A53, but it is a concurrency change across the whole consumer surface and belongs in its own decision. It would not substitute for D2 in any case: a retrying message holds its concurrency slot for the whole ladder, so more concurrency widens the head-of-line rather than removing it.

### D2 — Retry ladder per message class

Classification happens per **receive endpoint**, because that is the only level at which a retry filter can wrap the in-memory outbox (D1). Given Finding 5, the classes are:

| Class | Endpoints | Intervals | Total | Rationale |
|---|---|---|---|---|
| **SerializedInstance** | the single `Instance_<id>` queue: replication (`DbChangeSet`) plus every `IInstanceDependentCommand` / `IInstanceDependentEvent` | 200 ms, 1 s, 5 s | ≈ 6 s | The queue is strict FIFO with `PrefetchCount = 1`, so every retry stalls *all* instance-dependent work behind it, replication included. Transient SQLite contention clears in milliseconds. A durable failure must fault *fast*: a replication gap is then repaired by full-sync (ADR-003), which is cheaper and more correct than a long blocking ladder, and an instance-dependent command reports its failure through D6. |
| **Request** | instance-**in**dependent `IRequest` consumers, i.e. the shared `Requests` queue. An `IInstanceDependentRequest` resolves to the instance queue and takes that queue's ladder instead | 200 ms, 1 s, 3 s | ≈ 4 s | A backstop only: per D6a a request consumer answers with an error response instead of throwing, so it normally never reaches this ladder. The ladder covers what the catch cannot — a failing `RespondAsync`, a throwing `HandleException`. It stays well inside the caller's `RequestTimeout` (30 s), because a ladder that outlives the timeout burns CPU producing a response nobody is waiting for. |
| **Default** | `ICommand`, `IEvent`, activities, everything unclassified — in practice the shared `Commands` and `Events` queues | 200 ms, 1 s, 5 s | ≈ 6 s | Per Finding 5 these are shared, serialized queues, not per-message endpoints. A retry stalls all 28 command types behind it, so the budget is the same as the instance queue's. A command that cannot succeed in ≈6 s reports through D6 and dead-letters. |

Request endpoints are the only class that cannot be recognised from the queue name, so they are resolved once by scanning for consumers that consume requests *and nothing else* (`ConsumerTypeExtensions.ConsumesOnlyRequests`). The extra "and nothing else" matters: `MessagingHelper.ConsumesRequest` is an `All` over `MessagingHelper.FindMessageTypes`, which skips generic message types, so it is vacuously true both for a `ConsumerDefinition<T>` (no `IConsumer<>` at all) and for every `IConsumer<Fault<T>>`. Without the guard a fault consumer would be handed the shortest ladder in the table. The `Default` ladder is the fallback.

**Fail-safe direction.** An unrecognised endpoint gets *fewer* retries, not more. On a node where every queue is serialized, a ladder applied to the wrong endpoint costs head-of-line blocking for unrelated message types, which is worse than one missed retry of a message the transport still holds. Note where this bites: Request is the only class that cannot be read off the queue name, so it is the only one that can fall back — a request consumer registered from an assembly outside `assembliesToScan` silently takes the Default ladder. `MessageRetryPipelineTests.Should_apply_the_ladder_belonging_to_each_endpoint` pins all three outcomes, including that fallback.

**Precedence.** The instance queue is matched **first**, before the request set, and wins outright. It carries replication, and per Finding 5 it also carries every instance-dependent consumer — including instance-dependent *requests*, whose endpoint simply is that queue. Those are absent from the request set only because `IInstanceDependentRequest<T>` does not extend `IRequest<T>`; that is an accident of the contract hierarchy, not a guarantee, and a request-first order would let a later change to that interface silently move replication onto the shortest ladder. A queue's ladder follows its serialization property, never a single consumer that happens to sit on it.

**Why no long ladder anywhere.** The obvious objection is that ≈6 s will not ride out a PostgreSQL failover. It will not, and that is accepted: two of the three motivating scenarios do not apply. A broker reconnect is handled by the transport's own reconnect logic, not by the consumer retry filter — an unreachable broker stops the receive endpoint rather than faulting consumers. And Standalone, the constrained target, has neither a broker nor PostgreSQL; its SQLite contention clears in milliseconds. What remains is a local PostgreSQL restart on a master, and paying for it with a 51 s stall of every command on every node type is the wrong trade. Recovery windows beyond ≈6 s are D3's problem, and the day an endpoint is genuinely dedicated — a fault consumer, a courier activity, a message with its own `MessageEndpointAttribute` — a longer ladder becomes correct for it and this table should grow a fourth class.

**Exception classification.** Deterministic failures are not retried: `r.Ignore<ArgumentException>()` (covers `ArgumentNullException`, `ArgumentOutOfRangeException`, `InvalidEnumArgumentException`) and `r.Ignore<NotSupportedException>()`. A malformed payload or a programming error goes to `_error` immediately instead of consuming the whole ladder on a 2-core budget to fail identically four times, stalling the shared queue while it does. `MessageRetryPipelineTests.Should_not_retry_a_deterministic_failure` covers both ignored types, a derived one (`ArgumentNullException`, which the `Ignore<ArgumentException>()` above is claimed to cover), and an `InvalidOperationException` control that must still be retried — the rule is the one in this ADR most easily broken by a later edit, and the control is what stops the test from passing vacuously.

**Where the defaults live, and how to turn retry off.** The ladders above are `static readonly` arrays on `MessageRetryClassifier`; the matching `MessageBusOptions` properties are **nullable and default to `null`**. `ConfigurationBinder` **appends** bound array elements to a non-empty default array instead of replacing them, so a non-empty default on the property would silently turn an operator's three-step override into a six-step ladder that is the sum of both. A `null` default has no elements to append to, so a configured ladder replaces the default wholesale.

That leaves the empty array free to carry its own meaning, and it does — the three states are:

| Configuration | Meaning |
|---|---|
| absent, or `null` | use the class default from the table above |
| a non-empty array | use exactly that ladder |
| an **empty** array | **do not retry** — one attempt, then the message faults |

Both non-default states are reachable from a device, where configuration arrives as environment variables: `MessageBus__RetryIntervals__0=200` sets a ladder, and `MessageBus__RetryIntervals=` with an empty value binds to an empty array and disables retry. In JSON they are `[200]` and `[]`. An empty ladder installs **no** retry filter at all rather than a policy with zero intervals, so the endpoint behaves exactly as it did before this ADR.

### D3 — Delayed redelivery is not used, and here is exactly when it would be

**Decision: no `UseDelayedRedelivery`, no `UseScheduledRedelivery`.** The whole retry budget is carried in-process by D2.

- `UseDelayedRedelivery` requires the `rabbitmq_delayed_message_exchange` plugin. The deployed images (`rabbitmq:3-management` in `build/deploy.sh`/`build/review.sh`, `rabbitmq:4.0-management-alpine` in `tests/compose.master-slave.yaml`) do not ship it.
- `UseScheduledRedelivery` requires a message scheduler (Quartz/Hangfire). That is a second persistence store, a background thread pool and a stream of scheduler writes — all three are exactly what Edge-S cannot pay for.
- Neither exists on the in-memory transport, so adopting either would reintroduce the transport asymmetry that D1 removes.

**Revisit trigger.** Delayed redelivery becomes the right answer the moment the cluster broker is provisioned with the delayed-exchange plugin *and* a use case appears whose recovery window is minutes rather than seconds (e.g. waiting for an artifact repository to come back). At that point commands — and only commands — get a second-tier ladder of 1 min / 5 min / 15 min layered outside the D2 ladder. Until then, anything that cannot succeed within the D2 ladder is a dead-letter, by design.

### D4 — Kill switch policy

**Enabled on every receive endpoint, on both transports**, configured on the bus (`IBusFactoryConfigurator.UseKillSwitch`, which installs an observer per endpoint — it is not a pipe filter and therefore does not interact with the D1 ordering).

| Setting | Value | MassTransit default | Reason |
|---|---|---|---|
| `ActivationThreshold` | 5 | 100 | Messages that must be consumed in one tracking period before the switch arms. 100 is unreachable on an edge endpoint; the switch would never arm. At 5, with a 50 % trip threshold, it still takes at least three failures — not a coincidence. |
| `TripThreshold` | 50 % | 10 % | Conservative. A blip must not stop an endpoint; a systemic outage must. |
| `TrackingPeriod` | 5 min | 1 min | The window the two counters above are measured over. Sized so a *quiet* endpoint can reach the activation threshold at all — see below. |
| `RestartTimeout` | 60 s | 60 s | Self-healing without operator action, and the ceiling on the user-visible delay this decision accepts. |

**Counted per message, not per attempt.** The kill switch observes the endpoint's consume pipe from *outside* the retry filter: a message retried four times raises one `PreConsume` and one `ConsumeFault`. So a ladder that keeps a message for ~6 s produces one unit of evidence, not four, and the thresholds have to be read as message counts.

**The counters reset each tracking period.** They are not a running total — measured: with `ActivationThreshold` 10 and a 1 s tracking period, an endpoint fed a failing message every 200 ms consumed all 30 of them without ever tripping, because no single 1 s window ever accumulated 10. A cumulative counter would have tripped at the tenth.

**Activation is off by one, and the stop is not instant.** MassTransit arms the switch once `ActivationThreshold` messages have been consumed within the tracking period and trips it on the failure *after* that, so a persistently failing endpoint gets through `ActivationThreshold + 1` messages at minimum. Messages already dispatched when it trips still run to completion, so the exact number is not fixed — the guarantee is that the endpoint stops rather than draining its queue. `MessageRetryPipelineTests.Should_stop_an_endpoint_whose_messages_keep_failing` asserts that property rather than a count.

**Why the tracking period is five minutes and not MassTransit's one.** Activation is a rate, not a count: `ActivationThreshold` messages *within* `TrackingPeriod`. Two independent limits decide whether that is reachable, and the threshold has to clear both.

| Limit | What caps it | With the values above |
|---|---|---|
| **Arrival** | how many messages the endpoint receives. A quiet node's instance queue sees roughly one a minute, because health information is published on the health-check period (60 s). | 5 messages per 5 min — the threshold sits exactly on this bound |
| **Throughput** | how many it can *get through*. Under a sustained failure each delivery burns its whole D2 ladder before faulting, and `ConcurrentMessageLimit = 1` on Standalone means one delivery at a time per endpoint. | 300 s ÷ 6.2 s ≈ 48 per period — roughly 10× headroom |

**The two kill-switch numbers cannot be read independently of the D2 ladders.** The throughput limit is the reason: lengthen a ladder and an endpoint completes fewer deliveries per tracking period, which can push `ActivationThreshold` out of reach without anyone touching D4. That is not hypothetical — it is what the original combination did. At the first draft's 51 s default ladder and 10-in-60 s thresholds, a serialized Standalone endpoint managed about 1.2 deliveries a minute against a threshold of 10, so the switch could never arm on the Default class, and roughly 9.7 against 10 on the instance queue, so it could not arm there either. Only the Request class cleared the bar — on the low-traffic edge endpoint D4 is written for, the switch was dead. Shortening the ladders (D2) and widening the window fixed both ends of the same relationship. `MessageRetryClassifierTests.Should_let_the_kill_switch_arm_on_an_idle_edge_endpoint` asserts both bounds so the coupling cannot silently break again.

This also gives the switch a useful duty cycle. On a quiet endpoint it takes minutes of sustained failure to trip and then pauses for one minute — proportionate, because there is little backlog to protect. On a busy endpoint the window fills in seconds, so it trips almost immediately and spends most of its time stopped — which is where the protection is actually needed. The behaviour scales with load without a second set of knobs.

These numbers are a considered estimate from the health-check publish interval, not from production telemetry. The custom OTel meters in the observability phase are what would let them be set from measured per-endpoint message rates.

Rationale: when a dependency is down, the choice is between working through the backlog — each message burning its full ladder, then failing — and stopping for 60 s. On a 2-core node under an offline-first design, waiting is correct. What waiting *costs*, though, differs by transport, and the decision is right on both for different reasons:

- **RabbitMQ (master, slave).** The endpoint restarts itself and the messages stay in their durable queue, so pausing risks nothing: the alternative is draining the queue into `_error` at full CPU, which is both the CPU burn and the flash cost D5 bounds.
- **In-memory (Standalone / Edge-S).** There is no durable queue and no dead-letter queue. What a stopped endpoint leaves behind sits in the in-process message fabric and is lost on the next restart, power cycle or module apply — and a module apply is a *routine* event, since package operations are applied on the next restart. So here the pause does not protect the messages; it buys back CPU on a 2-core device, and it trades a certainty against a possibility. Continuing to consume destroys every message that exhausts its ladder (Finding 2: the in-memory error transport publishes to an exchange with no bound queue, which discards). Pausing keeps them in memory, where they are lost *only if* the node restarts inside the window — and delivered if the dependency recovers first.

Accepted trade-off: a stopped endpoint on Standalone (`ConcurrentMessageLimit = 1`) delays user-visible actions by up to 60 s, and those messages are held in memory rather than on disk, so a restart inside the pause loses them. Both are preferable to the alternative on that transport, which is not dead-lettering but silent destruction.

### D5 — Bounded, self-cleaning error queues

Applied through `IRabbitMqSendTopologyConfigurator.ConfigureErrorSettings` and `ConfigureDeadLetterSettings`, i.e. to `_error` **and** `_skipped`, on every node type including the master:

| Argument | Default | Purpose |
|---|---|---|
| `x-message-ttl` | 7 days | Fault evidence outlives one on-call rotation, then evaporates. |
| `x-expires` | 7 days | The queue itself disappears once it has been idle that long. |
| `x-max-length` | 250 messages | Hard message-count bound per endpoint. |
| `x-max-length-bytes` | 2 MiB | Hard byte bound per endpoint — the one that actually caps flash. |
| `x-overflow` | `drop-head` | On overflow, drop the **oldest**. `reject-publish` would make the error transport itself fail, turning a poison message into a redelivery loop. |

This converts the master's unbounded exposure into a hard ceiling of 2 MiB per endpoint that has ever faulted (worst case on a fully-loaded master, well under 100 MiB) and, in practice, zero — because error queues are only declared when something actually faults.

Note the arguments are applied *on top of* the ones copied from the input endpoint, so they win where the two disagree. On a non-master node that means a fault queue outlives the input queue it belongs to: the input queue carries `QueueLifetimeInDays` (5 days, capped by its `[Range]`) while its `_error` queue carries `RetentionInDays` (7). Accepted — a fault queue has no consumers, so `x-expires` reclaims it on its own schedule regardless of whether the node is still alive, and the two-day tail is a few KiB on the master's broker. The alternative, clamping retention to the input queue's lifetime, would make fault evidence expire *sooner* on exactly the nodes whose faults are hardest to reproduce.

Three settings on `ErrorQueueSettings` produce those five arguments: `RetentionInDays` drives both `x-message-ttl` and `x-expires`, `MaxMessages` drives `x-max-length`, and `MaxSizeInMegabytes` drives `x-max-length-bytes`. Setting one to `0` omits its argument, which is also the escape hatch for an existing broker (see *Consequences → Upgrade impact*). `x-overflow` is not configurable: it is emitted as `drop-head` whenever either length bound is present, for the reason in the table.

Nothing changes for Standalone: the in-memory transport discards faults and gains no retention. The kill switch and the D2 ladder are what protect it; a standalone node's post-mortem evidence is its journald log, which is why D6 requires the failure to be logged before it is rethrown.

### D6 — Retry *and* correlated failure feedback: the fault-consumer pattern

**Scope: fire-and-forget consumers only** — `ICommand`, `IInstanceDependentCommand` and `IEvent`, where the caller has already moved on and the outcome can only reach it as a separate, correlated event. Request/response is explicitly out of scope; see D6a.

The in-memory outbox makes "publish an error event and rethrow" impossible from inside such a consumer. The resolution is to move the feedback **out** of the consumer:

1. The consumer **logs the failure with the exception *and its correlation id*, then rethrows**. It publishes nothing on the failure path. It gets the full D2 ladder and, if that is exhausted, the message lands in `_error` (RabbitMQ) with all of its headers. The correlation id is not optional: on a transport that discards faulted messages this log line is the whole post-mortem (D5), and without it the stack trace cannot be tied back to the operation a user triggered. Where the message is instance-dependent, log the instance id too — the same failure on the same correlation id can occur on several nodes.
2. MassTransit publishes `Fault<T>` after the retries are exhausted. That publish happens in the error pipe, *outside* the faulted consumer's outbox scope, so it survives.
3. A dedicated `IConsumer<Fault<T>>` publishes the correlated, error-shaped completion event that the operator or the UI is waiting for, reading the correlation id from `context.Message.Message` (the original message is carried inside the fault).

Rules:

- **A fire-and-forget consumer whose failure must reach a human gets a fault consumer.** For those, correlated failure feedback is the fault consumer's job, never the catch block's.
- **Fault consumers are registered on master/standalone only** — a plain `IConsumer<Fault<T>>` is neither instance-dependent nor `[ReadOnlyConsumer]`, so `AddInstanceDependentActions` excludes it from a slave's main bus. It has to be kept off the slave's *local* bus as well: `AddLocalBus` selects consumers by the same request predicate, which is why that call site uses `ConsumesOnlyRequests` (see D2). Adding `[ReadOnlyConsumer]` to a fault consumer would produce one duplicate report per node and is prohibited.
- **Running on one node is not the same as reporting once.** Where the faulted command was *fanned out* — one copy per instance under a single correlation id, as `UpdateModulePackageOperationsConsumer` does — every failing node raises its own `Fault<T>`, and the single fault consumer on the master consumes all of them. A fault consumer that publishes a correlated, UI-facing event unconditionally therefore emits one per failing node, all claiming the same correlation id. Worse, the client completes a correlation on whichever verdict reaches it first (`CompleteWithError` and `CompleteWithSuccess` both remove the pending entry), so on a partial failure the master's success and a slave's failure *race* for the same operation. **A fault consumer for a fanned-out command must gate its correlated report on the fault belonging to the local node** (`command.InstanceId == instanceInfoProvider.Local.Id`); per-node facts stay unconditional on a separate, non-UI event carrying the instance id.
- **Fault consumers must be trivial**: read the fault, publish the correlated error event. No I/O, no state changes. They are the last line of the pipeline and must not themselves fault.
- The complementary rule — *never publish a success-shaped completion for failed work* — is already codified in the ADR-002 amendment of 2026-08-03 (*Pattern: never publish a success-shaped completion for failed work*) and is not restated here.
- **Documented exception:** `EventForwardToUiConsumer<T>` swallows per-subscriber failures by design. It fans one event out to N independent UI circuits; one dead circuit must not fail the other N-1, and there is no correlated caller to report to. Its failures are logged and it must never rethrow.

### D6a — Request/response answers with an error response, never a fault consumer

**Decision: request consumers keep catching, and do not get an `IConsumer<Fault<T>>`.**

`RequestConsumer<TRequest, TResponse>` and `InstanceDependentRequestConsumer<TRequest, TResponse>` (both in `Sdk.Backend.Messaging`) wrap `Respond` in a try/catch and answer with `HandleException(...)`, which every implementation uses to log the exception and return an error-shaped `TResponse` carrying an `ErrorInfo`. All 20 request consumers in `src/` follow this: 18 on the shared `Requests` queue in `Core.OS`, the instance-dependent `GetEnvironmentOverridesConsumer`, and `GetPasskeysConsumer` in `Blazor.Server.Backend`, which implements `IConsumer<GetPasskeys>` directly and reproduces the pattern by hand.

That already achieves what D6 exists for, and does it better:

- The failure reaches **the caller that is actually waiting**, addressed to its response address, synchronously, inside its `RequestTimeout`.
- A `Fault<T>` consumer could not: it is a published event, so it is not addressed to the waiting caller, and it fires only after the ladder is exhausted — by which point the caller has already seen a `RequestTimeoutException`. It would add a broadcast copy of an answer that had already been delivered privately.
- The error is typed. `ErrorInfo` is part of the response contract the client already handles, where `Fault<T>` would require the client to correlate a second, differently-shaped message.

**Accepted consequences.** A request consumer never throws, so on the normal path it is never retried, never dead-lettered and leaves nothing in `_error`. Its evidence is the log line written by `HandleException` and the `ErrorInfo` returned to the caller. This is accepted: a request is a read, the caller is told immediately, and a retry of a failing read is the caller's decision, not the bus's.

**The Request ladder is therefore a backstop, not the main path.** It only engages where the catch cannot: `RespondAsync` itself failing, a `HandleException` implementation that throws, or a future consumer that neither derives from the base class nor reproduces the pattern. Keeping it well inside `RequestTimeout` is what matters; its length is not what makes a caller see the fault.

**Rules:**

- A request consumer must **answer**, not throw: catch, log with the exception, respond with an `ErrorInfo`-carrying response. Deriving from `RequestConsumer<,>` / `InstanceDependentRequestConsumer<,>` gets this for free and is the default.
- A raw `IConsumer<TRequest>` must reproduce the pattern by hand.
- Request consumers must **not** ship an `IConsumer<Fault<T>>`. Adding one is a review defect.

### D7 — Circuit breaker and rate limiter

Not adopted. `UseCircuitBreaker` overlaps with D4 (the kill switch is the endpoint-level equivalent and additionally stops consuming instead of failing fast), and `UseRateLimit` solves a throughput problem the Suite does not have — `ConcurrentMessageLimit = 1` and `PrefetchCount = 1` already bound concurrency on the constrained targets.

---

## Alternatives Rejected

**Keep the retry policy on the bus factory instead of the endpoint.** Simpler, but it cannot express per-message-class ladders, and it forces the outbox to be configured at the same level. Rejected because D2 requires per-class ladders.

**Let consumers keep catching, and add a separate "notification" publish endpoint outside the outbox scope.** Would let a consumer both report and rethrow. Rejected: it gives every consumer a second, unaudited publish path that bypasses the outbox's atomicity guarantee, and it duplicates the report on every retry attempt.

**Consume `Fault<T>` on every node (`[ReadOnlyConsumer]`).** Rejected: `Fault<T>` is a published event, so every node with a bound queue receives it and would emit a duplicate correlated error event per node.

**Give error queues a dead-letter exchange chain instead of a TTL.** Rejected: it moves the storage problem one hop without bounding it, and it multiplies the number of durable queues on a broker that may run on constrained hardware.

**Delete `_error` messages after processing them with a fault consumer.** Rejected: `Fault<T>` and the `_error` queue are independent paths. Draining `_error` requires the shovel/management API and is an operator action, not an application one. Bounding it (D5) is sufficient.

---

## Consequences

### Positive

- Edge-S gains a retry policy where it previously had none — the single largest reliability gap in the messaging layer.
- Standalone and cluster deployments fail the same way, so a failure reproduced on a laptop reproduces on a device.
- Master brokers can no longer fill their disk with fault history.
- Failures that exhaust the ladder now produce both durable evidence (`_error`) and a correlated, operator-visible event (`Fault<T>` → fault consumer), instead of one or the other.
- A dependency outage costs a 60 s pause instead of a backlog worked through at 100 % CPU — drained into `_error` on RabbitMQ, destroyed outright on the in-memory transport (D4).

### Negative / accepted

- The command ladder grows from ≈3.6 s to ≈6 s, and Standalone gains one where it had none. A permanently poisoned command occupies the shared `Commands` queue for ≈6 s before dead-lettering; with `ConcurrentMessageLimit = 1` that is a real head-of-line delay for the other 27 command types, accepted because the alternative is losing the message. The kill switch caps the aggregate cost.
- Recovery windows longer than ≈6 s are explicitly out of scope until the delayed-exchange plugin is available (D3). A transient dependency outage that outlives the ladder dead-letters instead of recovering by itself.
- No endpoint gets a long ladder, including the ones that could safely have one today (fault consumers, courier activities). That is deliberate simplicity: a fourth "dedicated endpoint" class is worth adding only once dedicated endpoints are common enough to matter.
- The kill switch can stop an endpoint on a burst of unrelated failures. Bounded by `RestartTimeout` and observable through MassTransit's health checks.
- Error-queue arguments are part of a RabbitMQ queue's identity.
- The `[Range]` bounds on `KillSwitchSettings` and `ErrorQueueSettings` are now **enforced**, and an instance whose configuration violates one no longer starts. `AddSuiteOptions` takes a `[OptionsValidator]` source-generated validator as a required type argument instead of calling `ValidateDataAnnotations()`, so the `[ValidateObjectMembers]` markers that were inert — the generator is the only thing that honours them, and the repository previously had no validator at all — now make the validation recurse into the nested settings classes. A `MessageBus:KillSwitch:TripThresholdPercent` of `500` used to bind silently and leave the switch unable to trip; it is now a startup failure, covered by `SuiteOptionsValidationTests`. The bounds are unchanged: `TripThresholdPercent` is `[Range(1, 100)]` rather than starting at `0`, because a threshold of `0` is satisfied by any failure rate and would stop an endpoint on its first failure — and `0` is an easy value to reach for by accident, since `ErrorQueueSettings` in the same decision uses `0` to mean "omit this bound". `Enabled` is how the switch is turned off. What is accepted is the behaviour change this implies for operators: a value that was tolerated before now stops the instance at startup rather than degrading it silently, which is the trade this decision wants — see the CHANGELOG entry for `1.3.1`.
- `MessageBus:Connection` is **not** validated. It binds `MassTransit.RabbitMqTransportOptions`, a third-party type carrying no validation attributes; the suite does not annotate types it does not own, and the source generator emits nothing for a type with no annotated members. It is registered through `AddUnvalidatedSuiteOptions`, which records that exemption in code rather than leaving it to look like an oversight. A wrong host, port or credential therefore still surfaces the way it always has — as a broker connection failure at bus start, which the transport reports and retries — not as a configuration error at startup.

### Upgrade impact

**This is the one part of ADR-004 that needs an operator action before upgrading a broker that has ever dead-lettered a message.**

`_error` and `_skipped` queues that already exist on a broker were declared **without** the D5 arguments. Queue arguments are part of a RabbitMQ queue's identity, so redeclaring one with different arguments fails with `PRECONDITION_FAILED` and closes the channel. Because those queues are created lazily, on the first fault of an endpoint, a deployment that has never faulted has none and is unaffected — but a master keeps them forever (that is the exposure D5 exists to close), so any master that has faulted once since its broker was provisioned *does* have them.

Where they exist, the upgrade path is one of:

1. delete the affected `*_error` / `*_skipped` queues (management UI or `rabbitmqctl`), or
2. set `MessageBus__CleanVirtualHost='true'` once, or
3. set the D5 values to `0` to omit the arguments entirely.

The declaration is attempted lazily, when a faulted message is first moved — not at startup — so a node upgraded without doing one of the above starts and runs normally, and the mismatch only surfaces the next time something faults. What happens to *that* message has not been verified against a broker: the move to `_error` fails, and whether MassTransit then discards it, redelivers it, or stalls the endpoint depends on transport behaviour this ADR did not test. Treat the cleanup as required rather than best-effort until someone has measured it. A test would need a real broker and belongs to the integration tier (review plan Phase 9).

---

## Compliance

- Every new fire-and-forget `IConsumer<T>` that reports a correlated outcome must either succeed-and-publish or throw; publishing an error-shaped event from a catch block instead of rethrowing is a review defect (D6).
- Fire-and-forget consumers that need correlated failure feedback ship with an `IConsumer<Fault<T>>` in the same commit.
- Request consumers do the opposite: they answer with an `ErrorInfo`-carrying response and never throw, and shipping an `IConsumer<Fault<T>>` for a request is a review defect (D6a).
- A fault consumer for a command that is fanned out per instance gates its correlated, UI-facing report on the fault belonging to the local node. Publishing it for every fault is a review defect (D6): it duplicates the report per failing node and races the local node's own verdict for the same correlation id.
- Fault consumers must not carry `[ReadOnlyConsumer]`.
- `MassTransitConfiguration.cs` remains the only place where retry, redelivery, outbox, kill-switch and error-queue behaviour is *configured* — `FaultQueueTopology` holds the D5 queue arguments it applies, and `MessageRetryClassifier` the D2 ladders and their selection, but nothing else may attach these filters. A `UseMessageRetry` inside a `ConsumerDefinition<T>` **nests** inside the endpoint-level ladder and multiplies the attempt count, so per-consumer retry overrides are not permitted; adjust the class ladder in `MessageBusOptions` instead.
- The D6 and D6a rules are enforced by tooling as well as review: `create-consumer` picks the error-handling shape from the message kind, `pre-mr-review` §4 checks it on every changed consumer, and `review-module` audits it per module.

---

## Amendments

_None yet._
