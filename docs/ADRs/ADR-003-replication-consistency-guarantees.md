# ADR-003: Replication Consistency Guarantees

## Status

Accepted

## Date

2026-06-04

## Context

ViciOne Suite replicates master-side state to slave nodes via an asynchronous pipeline:

```
ChangeTrackingInterceptor → DbChangeSet publish → RabbitMQ transport → DbChangeSetConsumer → SQLite apply
```

The system claims **eventual consistency** with **master-wins** conflict resolution (see `suite-architecture.md`). This ADR documents the concrete guarantees, failure modes, and ordering assumptions at each pipeline stage — and identifies gaps that require hardening.

### Constraints

- Edge-S hardware: ARM Cortex-A53, 1.9 GB RAM, 2.3 GB flash (limited write cycles)
- Slaves use in-memory SQLite (no WAL, no disk persistence by default)
- RabbitMQ is the sole inter-node transport (durable queues, fan-out exchanges)
- MassTransit with in-memory outbox (RabbitMQ mode) or in-memory bus (standalone)
- `PrefetchCount = 1` and `ConcurrentMessageLimit = 1` on `DbChangeSetConsumer`

## Pipeline Stage Analysis

### Stage 1: ChangeTrackingInterceptor (Master-side capture)

**Location:** `Core.OS/Persistence/ChangeTrackingInterceptor.cs`

**Mechanism:**
1. `SavingChangesAsync` — scans EF Core `ChangeTracker`, serializes modified entities to JSON, stages them in a `ConcurrentQueue<List<ChangedEntity>>`.
2. `SavedChangesAsync` — fires **after** the DB transaction commits successfully. Dequeues staged changes, constructs a `DbChangeSet`, publishes via `ISuiteMediator.Publish()`.
3. `SaveChangesFailedAsync` / `SaveChangesCanceledAsync` — discard staged changes (no publish).

**Guarantees:**
- ✅ Publish only happens after successful commit (no phantom changesets)
- ✅ Change count validation: throws if staged count ≠ saved count
- ✅ Only registered on Master (`InstanceType.Master` check in DI registration)
- ✅ Filtered entities: `NotSynchronizedEntityTypes` and navigation relations excluded
- ✅ Batch atomicity: all entities from a single `SaveChangesAsync` call form one `DbChangeSet`

**Failure modes:**
- ❌ **Post-commit publish failure**: If `mediator.Publish()` throws (broker unreachable), the DB transaction is already committed but the changeset is lost. The in-memory outbox (`UseInMemoryOutbox`) only applies within consumer pipelines, **not** when publishing from application code outside a consumer context.
- ❌ **Process crash between commit and publish**: Same result — committed data never reaches slaves.
- ⚠️ **Bulk operations bypass**: `ExecuteUpdate`/`ExecuteDelete` do not trigger change tracker and are not intercepted. Documented as known limitation.
- ⚠️ **Shadow properties**: Not serializable, not replicated. Documented as known limitation.
- ⚠️ **Non-serializable types**: Types that fail JSON serialization silently produce `null` entities (filtered out in `SavedChangesAsync`).

**Ordering:**
- Changes within a single `SaveChangesAsync` are captured in `ChangeTracker.Entries()` enumeration order (deterministic but unspecified by EF Core — typically insertion order).
- Multiple `SaveChangesAsync` calls within a request produce multiple `DbChangeSet` messages published sequentially.

---

### Stage 2: DbChangeSet Publish (Master → RabbitMQ)

**Location:** `Core.OS/MessageBus/BackEndMediator.cs` → `IPublishEndpoint.Publish()`

**Mechanism:**
- `DbChangeSet` implements `IInstanceEvent` with `[MessageEndpoint("DbReplication")]`
- Published via MassTransit's `IPublishEndpoint` → RabbitMQ fan-out exchange
- Each registered slave has a durable queue bound to this exchange (named `DbReplication_{InstanceId}`)

**Guarantees:**
- ✅ RabbitMQ publisher confirms (MassTransit default with RabbitMQ transport) — `Publish()` awaits broker acknowledgement
- ✅ Fan-out ensures every connected slave's queue receives the message
- ✅ Durable queues survive broker restarts
- ✅ Queue TTL: `x-expires` set to `QueueLifetimeInDays` (default 5 days) — prevents unbounded growth for permanently removed nodes

**Failure modes:**
- ❌ **Broker unreachable at publish time**: `Publish()` throws, application code receives exception. Since publish is outside the outbox pattern (application-level, not consumer-level), no automatic retry exists. The changeset is lost.
- ⚠️ **Broker accepts but queue is full/blocked**: RabbitMQ flow control can block the publishing connection. MassTransit respects this and backs off, but the master's business operation may time out.
- ⚠️ **Exchange exists but no queues bound**: If a slave has never connected (queue not yet created), messages published to the exchange are discarded by RabbitMQ (no binding = message lost for that slave). Full-sync on first registration handles this correctly.

**Ordering:**
- Messages published sequentially from a single connection maintain FIFO order in RabbitMQ.
- Multiple concurrent `SaveChangesAsync` calls (from different HTTP requests) produce interleaved publish calls on a shared `IPublishEndpoint`. RabbitMQ guarantees per-channel ordering, and MassTransit uses a single channel per publish endpoint → **global FIFO is maintained**.

---

### Stage 3: RabbitMQ Transport (Queue storage and delivery)

**Mechanism:**
- One durable queue per slave per message type: `DbReplication_{InstanceId}`
- Messages persist in queue until consumed or TTL expires
- Delivery uses AMQP `basic.deliver` with manual acknowledgement

**Guarantees:**
- ✅ At-least-once delivery: messages are redelivered if consumer does not ack
- ✅ FIFO within a single queue (single consumer, `PrefetchCount = 1`)
- ✅ Persistence: messages survive broker restart (durable queue + persistent delivery mode)
- ✅ Automatic queue expiration after `QueueLifetimeInDays` of inactivity

**Failure modes:**
- ❌ **Queue TTL expiration**: If a slave is offline > 5 days, the queue (and all pending messages) is deleted by RabbitMQ. The slave must detect this and trigger a full-sync on reconnection.
- ❌ **Broker data loss**: If RabbitMQ loses its mnesia database (disk failure, misconfigured HA), all queued messages are lost. Slaves will be silently out-of-sync until full-sync is triggered.
- ⚠️ **Message TTL vs queue TTL**: Individual messages do not have a per-message TTL. If the queue survives but a message has been sitting for days, it will still be delivered (correct behavior — stale-but-ordered delivery is better than dropping).
- ⚠️ **Network partition (split-brain)**: RabbitMQ clustering split-brain can cause message duplication or loss depending on partition handling mode. ViciOne does not configure `ha-mode` or quorum queues explicitly.

**Ordering:**
- ✅ Strict FIFO per queue (guaranteed by RabbitMQ for single-consumer queues)
- ✅ `PrefetchCount = 1` ensures only one message is in-flight at a time
- ⚠️ No cross-queue ordering guarantees (each slave's queue is independent — correct, as slaves are independent)

---

### Stage 4: DbChangeSetConsumer (Slave-side apply)

**Location:** `Core.OS/Persistence/Consumers/DbChangeSetConsumer.cs`

**Mechanism:**
1. Resolve target `DbContext` by `ContextType` string
2. For each `ChangedEntity` in the batch:
   - Deserialize JSON → entity object
   - Extract primary key values
   - `Deleted`: FindAsync by PK → Remove if exists
   - `Added`/`Modified`: FindAsync by PK → Update if exists, Add if not (upsert semantics — ADR-002)
3. Handle connection-tag relationship fixups (`FixConnectionRelation`)
4. `SaveChangesAsync()` — single transaction for entire batch

**Guarantees:**
- ✅ Idempotent: upsert semantics make redelivery safe (ADR-002)
- ✅ Batch atomicity: all changes in a `DbChangeSet` are applied in a single `SaveChangesAsync` call — all-or-nothing
- ✅ Sequential processing: `PrefetchCount = 1` + `ConcurrentMessageLimit = 1` prevents concurrent applies
- ✅ Startup dependency: `SynchronizationState` blocks regular message processing until initial sync completes

**Failure modes:**
- ❌ **Partial deserialization failure**: If one entity in the batch fails to deserialize (type not loaded, assembly mismatch), the entire batch fails. After retries exhaust, the message goes to `_error` queue. All subsequent messages in the queue are blocked until manually resolved.
- ❌ **Schema mismatch**: If master has migrated but slave hasn't (version drift), `SaveChangesAsync` throws `DbUpdateException`. Same blocking behavior as above.
- ❌ **Type not found**: If `GetChangeSetDbContext` returns `null` (module uninstalled on slave), the consumer returns early without processing. Changes for that context are silently dropped. Subsequent messages for other contexts are processed normally.
- ⚠️ **Out-of-order after error queue resolution**: If a message is moved to `_error` queue and later reprocessed, it arrives after newer messages have already been applied. This can cause stale overwrites for `Modified` changes.
- ⚠️ **Entity serialization fidelity**: Only public properties serialized by `System.Text.Json` are replicated. Private fields, computed properties, and EF Core shadow properties are lost in transit.

**Ordering:**
- ✅ FIFO processing guaranteed by `PrefetchCount = 1`
- ⚠️ No sequence number or version vector: consumer cannot detect gaps (missing messages between successfully processed ones)
- ⚠️ No conflict detection: last-write-wins by design (acceptable given master-wins topology)

---

### Stage 5: Full-Sync Path (Alternative to incremental replication)

**Location:** `Core.OS/Instance/Initialization/SyncDataActivity.cs`

**Mechanism:**
- Triggered when: new instance registers OR instance reconnects after > `QueueLifetimeInDays`
- Executed via MassTransit Routing Slip (sequential activities per module per table)
- For each table: `DELETE FROM {table}` → `INSERT INTO {table} VALUES (...)` using raw SQL
- Final activity sets `SyncCompleted = true` → unblocks regular message processing

**Guarantees:**
- ✅ Naturally idempotent (DELETE + INSERT produces same result regardless of prior state)
- ✅ Foreign keys disabled during sync (`PRAGMA foreign_keys = OFF`)
- ✅ `SynchronizationState` dependency blocks all other consumers until sync completes
- ✅ Routing slip ensures ordered execution across tables

**Failure modes:**
- ❌ **Partial routing slip failure**: If one `SyncDataActivity` execution fails mid-way, the routing slip faults. Some tables are synced, others are not. There is no compensating transaction — the slave is left in an inconsistent state.
- ❌ **Changes on master during full-sync**: While the routing slip is executing (which can take significant time for large datasets), the master may commit new changes. These are published to the slave's queue but blocked by `SynchronizationState`. Once sync completes, the incremental messages are applied — but they may reference entities that were already part of the full-sync snapshot, causing redundant upserts (harmless due to idempotency).
- ⚠️ **No atomicity across tables**: Each table is synced independently. Cross-table referential integrity may be temporarily violated during the sync window.

---

## Identified Gaps

### Gap 1: Post-Commit Publish Failure (Critical)

**Problem:** The `ChangeTrackingInterceptor` publishes `DbChangeSet` after `SaveChangesAsync` succeeds. If the publish fails (broker down, network issue), the master DB has the data but no slave will ever receive it via incremental replication.

**Impact:** Silent data divergence. Slaves permanently miss the change until a full-sync is triggered.

**Current mitigation:** None. The `ISuiteMediator.Publish()` call is fire-and-forget from the interceptor's perspective — it `await`s the publish but does not retry on failure, and the interceptor has no retry mechanism.

**Recommendation:** Implement MassTransit's EF Core **Bus Outbox** (`AddEntityFrameworkOutbox<T>` + `UseBusOutbox()`) on the master's PostgreSQL DbContext. The Bus Outbox replaces the scoped `IPublishEndpoint` with a version that writes to an `OutboxMessage` table instead of directly to the broker. When the application calls `SaveChangesAsync()`, the outbox messages are committed atomically with the business data. A background delivery service then relays messages to RabbitMQ asynchronously.

**Required architectural change:** Restructure `ChangeTrackingInterceptor` to publish the `DbChangeSet` DURING `SavingChangesAsync` (while the transaction is open) rather than in `SavedChangesAsync` (after commit). This allows the Bus Outbox to capture the message within the same DB transaction. The `MassTransit.EntityFrameworkCore` package is already in `Directory.Packages.props` at v8.5.9. See Phase 2b for implementation.

### Gap 2: No Gap Detection on Slave (High)

**Problem:** The `DbChangeSetConsumer` has no way to detect that a message was lost (e.g., broker data loss, queue expiration while messages were pending). It processes whatever arrives without validating continuity.

**Impact:** Silent gaps in replication. A slave may miss intermediate changes, leading to stale state that is never corrected until an unrelated full-sync occurs.

**Recommendation:** Add a monotonically increasing sequence number per slave queue to `DbChangeSet`. The consumer validates sequential continuity and triggers a full-sync request if a gap is detected.

### Gap 3: Error Queue Blocking (High)

**Problem:** When a single `DbChangeSet` message fails permanently (after retries exhaust), it moves to the `_error` queue. All subsequent messages for that slave continue to arrive but the consumer continues processing. However, if the failure is due to a deserialization error of one entity in the batch, the entire batch (which may contain changes for multiple entities) is lost.

**Impact:** Data loss for the batch, potential cascading inconsistency if later messages reference entities that should have been created in the failed batch.

**Recommendation:** Implement per-entity error isolation: if one entity in the batch fails to deserialize, skip it and log a warning, then continue processing remaining entities. Move only the failed entity to an error log, not the entire batch.

### Gap 4: Queue Recreation Detection (Medium)

**Problem:** If a slave's queue expires (offline > 5 days) and the slave reconnects, the `RegisterInstanceConsumer` correctly triggers a full-sync based on `LastRegistered` timestamp comparison. However, if the broker loses just the queue data (not the queue definition) — e.g., after a broker failover without HA — the slave reconnects to an empty queue without triggering full-sync, because `LastRegistered` appears recent.

**Impact:** Silent gap in replication history. Slave appears in-sync but has missed messages queued between the data loss and reconnection.

**Recommendation:** Include a "last-applied sequence number" in the slave's registration payload. The master compares against its published sequence to detect gaps.

### Gap 5: No Replication Lag Observability (Medium)

**Problem:** There is no mechanism to measure or alert on replication lag. Operators cannot determine whether a slave is seconds or hours behind the master.

**Impact:** Operational blindness. Issues (network problems, slow consumers) are invisible until users notice stale data.

**Recommendation:** Publish a timestamp in each `DbChangeSet`. The slave compares against local time and exposes replication lag via the existing health check infrastructure.

### Gap 6: Routing Slip Partial Failure (Medium)

**Problem:** A full-sync routing slip executes multiple `SyncDataActivity` steps sequentially. If one fails, the routing slip faults and `SynchronizationState` is never completed. The slave is stuck — it cannot process any messages.

**Impact:** Slave is permanently non-functional until manual intervention (restart with full-sync retry).

**Recommendation:** Add fault handling to the routing slip: on `RoutingSlipFaulted`, log the failure and either retry the full-sync or mark the slave as degraded in the cluster health info.

## Decision

Document the replication guarantees as stated above. Prioritize gap remediation in Phase 2b:

| Gap | Priority | Phase 2b Scope |
|-----|----------|----------------|
| Gap 1: Post-commit publish failure | Critical | Transactional outbox or publish retry with idempotent delivery |
| Gap 2: No gap detection | High | Sequence numbers + gap-triggered full-sync |
| Gap 3: Error queue blocking | High | Per-entity error isolation in consumer |
| Gap 4: Queue recreation detection | Medium | Sequence number exchange during registration |
| Gap 5: No replication lag observability | Medium | Timestamp in changeset + health check metric |
| Gap 6: Routing slip partial failure | Medium | Fault observer + automatic retry |

## Alternatives Considered

| Alternative | Why not adopted (yet) |
|-------------|----------------------|
| Kafka/event log for replication | Eliminates ordering/gap issues but introduces infrastructure dependency incompatible with Edge-S constraints |
| PostgreSQL logical replication | Tight coupling to Postgres, incompatible with SQLite slaves |
| CRDTs for conflict-free state | Massive refactoring, overkill for master-wins topology |
| Custom outbox table (manual) | MassTransit already provides first-party EF Core Bus Outbox with delivery service — no need to reinvent |
| In-memory outbox (current) | Only works within consumer pipelines; does not cover application-level publishes from interceptors |
| Explicit out-of-order handling after slave restart | Not needed. RabbitMQ queues preserve FIFO order — messages buffered during downtime (up to 5-day TTL) are delivered sequentially on reconnect. Combined with `PrefetchCount=1`, delivery order is guaranteed. The in-memory reorder buffer resets naturally on restart (singleton lifecycle); the first received message establishes the sequence baseline. Full-sync is only required when downtime exceeds the 5-day queue TTL, causing message expiry. |

## Consequences

- This ADR establishes the **documented baseline** for replication guarantees. Any system claiming "data is synced to slaves" must be understood within these constraints.
- Operators must be aware that **post-commit publish failure** is a silent data-loss scenario today.
- The 5-day queue TTL is correct for the normal case but **does not cover broker-side data loss**.
- Phase 2b will implement the hardening measures for Critical and High gaps.

## Implementation (Phase 2b)

**Branch:** `Phase-2` (target: `v1.3.0`)
**Commits:** `f3ed754c`..`1cbb34e8` (2026-06-08 – 2026-06-09)

### Updated Pipeline Diagram

```
Master (PostgreSQL)                           Slave (SQLite in-memory)
─────────────────────                         ────────────────────────

EF Core SaveChangesAsync
        │
        ▼
┌───────────────────────────────┐
│ ChangeTrackingInterceptor     │
│ (SavingChangesAsync)          │
│                               │
│  • Serialize entities to JSON │
│  • Assign SequenceNumber      │ ◄── ReplicationSequenceCounter (singleton)
│  • Stamp PublishedAt = UtcNow │
│  • Publish DbChangeSet        │ ◄── Bus Outbox captures in same DB txn
└───────────────┬───────────────┘
                │
                ▼
┌───────────────────────────────┐
│ OutboxDbContext (schema:outbox)│
│                               │
│  • OutboxMessage table        │ ◄── Atomic with business data commit
│  • OutboxState table          │
│  • InboxState table           │
└───────────────┬───────────────┘
                │ (MassTransit delivery service)
                ▼
┌───────────────────────────────┐
│ RabbitMQ fan-out exchange     │
│ (DbReplication)               │
│                               │
│  Per-slave durable queue      │
│  (TTL: QueueLifetimeInDays)   │
└───────────────┬───────────────┘
                │
                ▼
┌───────────────────────────────┐
│ DbChangeSetConsumer           │
│ (PrefetchCount=1, Conc=1)     │
│                               │
│  1. await SynchronizationState│
│  2. Submit to SequenceTracker │ ◄── ReplicationSequenceTracker
│  3. For each message to apply:│
│     • Per-entity try/catch    │
│     • Upsert/Delete semantics │
│  4. SaveChangesAsync          │
│  5. Record lag                │ ◄── ReplicationLagTracker
│  6. If gap timeout → resync   │
└───────────────────────────────┘
```

### 2b.1: Transactional Outbox (Gap 1 — Critical)

**Problem solved:** Post-commit publish failure caused silent data divergence.

**Solution:** MassTransit EF Core Bus Outbox with a dedicated `OutboxDbContext`.

**Key changes:**

| File | Change |
|------|--------|
| `Core.OS/DbContext/OutboxDbContext.cs` | New DbContext for outbox tables (schema: `outbox`) |
| `Core.OS/Migrations/OutboxDbContext/Postgres/20260608132737_InitialOutbox.cs` | Migration creating `InboxState`, `OutboxMessage`, `OutboxState` tables |
| `Core.OS/Persistence/ChangeTrackingInterceptor.cs` | Moved publish from `SavedChangesAsync` to `SavingChangesAsync` |
| `Core.OS/Persistence/BusOutboxReplicationPublisher.cs` | Stages the change set in the outbox on the module's own connection and transaction |
| `Core.OS/MessageBus/MassTransit/Configuration/MassTransitConfiguration.cs` | Registered `AddEntityFrameworkOutbox<OutboxDbContext>` plus the delivery half of the Bus Outbox |

**Configuration (master + RabbitMQ only):**

```csharp
// MassTransitConfiguration.cs — inside UsingRabbitMq block
if (useBusOutbox) // master and not the in-memory bus
{
    services.AddDbContext<OutboxDbContext>((sp, o) =>
        o.UseNpgsql(sp.GetRequiredService<IMasterDbConnectionStringProvider>().ConnectionString));

    busConfig.AddEntityFrameworkOutbox<OutboxDbContext>(o => o.UsePostgres());
    busConfig.AddBusOutboxDelivery();
}
```

`UseBusOutbox()` is deliberately not called.
It registers the notification and the delivery service, but also replaces the scoped bus context with one that
diverts every send and publish into the outbox change tracker whenever the scope carries no `ConsumeContext`.
Replication is the only thing that stages into the outbox, and `BusOutboxReplicationPublisher` does so explicitly,
so `AddBusOutboxDelivery()` takes just the delivery half and everything else keeps reaching the transport directly.
`MasterBusOutboxTests` pins that split, including the delivery options `UseBusOutbox()` would have configured.

**Outbox table schema (PostgreSQL, schema `outbox`):**

| Table | Key Columns | Purpose |
|-------|-------------|---------|
| `OutboxMessage` | `SequenceNumber` (PK, identity), `MessageId`, `Body`, `SentTime` | Buffered messages awaiting delivery |
| `OutboxState` | `OutboxId` (PK), `Created`, `Delivered`, `LastSequenceNumber` | Tracks per-scope delivery state |
| `InboxState` | `Id` (PK), `MessageId`+`ConsumerId` (unique), `Received`, `Consumed` | Consumer-side deduplication |

**Behavioral change:** `ChangeTrackingInterceptor` now stages during `SavingChangesAsync` (transaction open).
`BusOutboxReplicationPublisher` opens its `OutboxDbContext` on the module's own connection and enlists it in the
module's transaction, so the `OutboxMessage` row is written within the same DB transaction as the business data and
a rolled-back save takes the staged row with it.
MassTransit's background delivery service relays to RabbitMQ asynchronously.
On standalone (in-memory bus), publish goes directly without outbox.

---

### 2b.2: Sequence Numbers (Gap 2 — High)

**Problem solved:** No gap detection — slaves couldn't identify missing messages.

**Solution:** Monotonically increasing per-context sequence numbers + in-memory reorder buffer.

**Key changes:**

| File | Change |
|------|--------|
| `Core.OS/Persistence/DbChangeSet.cs` | Added `long SequenceNumber` and `DateTimeOffset PublishedAt` properties |
| `Core.OS/Persistence/ReplicationSequenceCounter.cs` | Master-side singleton: `ConcurrentDictionary<string, long>` with atomic increment |
| `Core.OS/Persistence/ReplicationSequenceTracker.cs` | Slave-side reorder buffer with gap detection and timeout-based flush |
| `Core.OS/Persistence/Consumers/DbChangeSetConsumer.cs` | Integrates tracker; triggers full-sync on unresolvable gaps |

**`DbChangeSet` record (updated):**

```csharp
[MessageEndpoint("DbReplication")]
public sealed record DbChangeSet(
    List<ChangedEntity> Changes,
    string ContextType,
    long SequenceNumber,       // NEW: monotonic per ContextType
    DateTimeOffset PublishedAt  // NEW: UTC timestamp at publish
) : IInstanceEvent;
```

**ReplicationSequenceTracker behavior:**

| Scenario | Action |
|----------|--------|
| First message after reset (seq tracker empty) | Accept as baseline, apply |
| `seq == lastApplied + 1` | Apply + drain contiguous buffered messages |
| `seq <= lastApplied` | Skip (duplicate/already-applied) |
| `seq > lastApplied + 1` (gap) | Buffer message |
| Buffer timeout (5s) or overflow (1000 msgs) | Flush all buffered, trigger full-sync |

**Full-sync trigger path:**

```
Gap timeout → DbChangeSetConsumer.TriggerFullSync()
  → synchronizationState.Reset()       // block further messages
  → Send RegisterInstance { ForceSync = true }
  → sequenceTracker.Reset()            // accept next message as baseline
```

**Full-sync completion reset:**

```
SyncDataActivity (SyncCompleted = true)
  → ReplicationSequenceTracker.Reset()  // clear all context state
  → SyncRetryState.Reset()             // clear failure counter
  → SynchronizationState.CompleteSynchronization()  // unblock messages
```

---

### 2b.3: Per-Entity Error Isolation (Gap 3 — High)

**Problem solved:** Single entity deserialization failure dropped entire batch.

**Solution:** Try/catch per entity; skip failed entities, apply successful ones.

**Key change in `DbChangeSetConsumer.ApplyChangeSet()`:**

```csharp
foreach (var change in changeSet.Changes)
{
    try
    {
        ApplyEntity(dbContext, change);
        appliedCount++;
    }
    catch (Exception ex)
    {
        LogEntityApplyFailed(logger, ex, change.EntityTypeFullName,
            change.State.ToString(), changeSet.ContextType);
    }
}

// Only throw if ALL entities failed (preserves MassTransit retry for total failures)
if (appliedCount == 0 && lastEntityException is not null)
    throw new InvalidOperationException(...);
```

**Log message (structured, Error level):**
```
Replication entity skipped: failed to apply entity '{EntityType}' (state: {EntityState})
in context '{ContextType}'. This entity will remain divergent until a full-sync is triggered.
```

**Trade-off:** Skipped entities create permanent per-entity divergence. The sequence tracker won't detect this (the message was consumed). Monitoring must alert on these structured log entries.

---

### 2b.4: Replication Lag Observability (Gap 5 — Medium)

**Problem solved:** No visibility into how far behind slaves are.

**Solution:** Timestamp in `DbChangeSet` + health check with configurable thresholds.

**Key changes:**

| File | Change |
|------|--------|
| `Core.OS/Persistence/ReplicationLagTracker.cs` | Singleton tracking per-context lag (`Math.Max(0, lag)` for clock skew) |
| `Core.OS/Instance/HealthCheck/ReplicationLagHealthCheck.cs` | `IHealthCheck` with degraded/unhealthy thresholds |
| `Core.OS/Instance/Extensions/IHealthChecksBuilderExtensions.cs` | Registered for `InstanceType.Slave` only |

**Health check thresholds:**

| Status | Condition |
|--------|-----------|
| Healthy | All contexts < 30s lag |
| Degraded | Any context ≥ 30s and < 5min |
| Unhealthy | Any context ≥ 5min |

**Health check output includes:** per-context lag in seconds as structured data.

**Assumption:** NTP synchronization between master and slave. Negative lag values (clock skew) are clamped to zero.

---

### 2b.5: Routing Slip Fault Handling (Gap 6 — Medium)

**Problem solved:** Full-sync failure left slaves permanently stuck.

**Solution:** `SyncRoutingSlipFaultedConsumer` + `SyncRetryState` + `SyncRetryHealthCheck`.

**Key changes:**

| File | Change |
|------|--------|
| `Core.OS/Instance/Consumers/SyncRoutingSlipFaultedConsumer.cs` | Consumes `InstanceSynchronizationFailed`; resets sync state; schedules retry |
| `Core.OS/Instance/Services/SyncRetryState.cs` | Thread-safe retry counter with configurable limit |
| `Core.OS/Instance/HealthCheck/SyncRetryHealthCheck.cs` | Reports degraded (retrying) or unhealthy (exhausted) |
| `Core.OS/Instance/Initialization/SyncDataActivity.cs` | Resets `SyncRetryState` on successful completion |

**Recovery flow:**

```
InstanceSynchronizationFailed received
  → SynchronizationState.Reset()       // re-block messages
  → SyncRetryState.RecordFailure()
  → If retries remaining (< 3):
      → Task.Delay(10s)
      → Send RegisterInstance { ForceSync = true }
  → If retries exhausted:
      → Log Critical
      → SyncRetryHealthCheck reports Unhealthy
      → Manual intervention required
```

**Configuration:**

| Parameter | Default | Description |
|-----------|---------|-------------|
| `MaxRetries` | 3 | Consecutive failures before marking degraded |
| `RetryDelay` | 10 seconds | Delay between retry attempts |

**Consumer registration:** Exempt from `SynchronizationState` dependency (must receive messages while sync is pending). Marked `[ReadOnlyConsumer]` — registered on slave instances.

---

### Gap 4: Queue Recreation Detection — ADDRESSED (Phase 2c)

**Status:** Implemented.

**Problem solved:** Master restart caused in-memory `ReplicationSequenceCounter` to reset to 0. Running slaves with `lastApplied = N` silently skipped all new messages (seq 1, 2, 3... all ≤ N).

**Solution:** Three-pronged approach:
1. **Persistent counter:** `ReplicationSequenceState` table in the outbox schema persists the counter atomically on each publish.
2. **Startup seeding:** On master startup, counter is seeded from the persisted state table.
3. **Sequence exchange during registration:** Slaves report their `LastAppliedSequences` in the `RegisterInstance` command. Master compares against its own counter to detect mismatches.

**Key changes:**

| File | Change |
|------|--------|
| `Core.OS/Persistence/ReplicationSequenceState.cs` | New entity: `ContextType` (PK) + `LastSequenceNumber` |
| `Core.OS/Persistence/ReplicationSequenceCounter.cs` | Added `Seed()` and `GetAll()` methods |
| `Core.OS/Persistence/ReplicationSequenceTracker.cs` | Added `GetAllLastApplied()` method |
| `Core.OS/Persistence/ChangeTrackingInterceptor.cs` | Persists counter to PostgreSQL on each publish (atomic upsert) |
| `Core.OS/DbContext/OutboxDbContext.cs` | Added `DbSet<ReplicationSequenceState>` |
| `Core.OS/Instance/Commands/RegisterInstance.cs` | Added `Dictionary<string, long> LastAppliedSequences` |
| `Core.OS/Modules/Services/ApplicationWorker.cs` | Populates `LastAppliedSequences` from tracker; seeds counter on startup |
| `Core.OS/Instance/Consumers/RegisterInstanceConsumer.cs` | Compares slave sequences against master counter; fixed `<` → `<=` boundary |
| `Core.OS/Persistence/Consumers/DbChangeSetConsumer.cs` | `TriggerFullSync` includes `LastAppliedSequences` |
| `Core.OS/Migrations/OutboxDbContext/Postgres/20260609120000_AddReplicationSequenceState.cs` | Migration for new table |

**Counter persistence mechanism:**

```csharp
// In ChangeTrackingInterceptor.SavingChangesAsync — before publishing the DbChangeSet:
// Executes in auto-commit mode on the same connection. On PostgreSQL only (provider check).
INSERT INTO outbox."ReplicationSequenceState" ("ContextType", "LastSequenceNumber")
VALUES (@contextType, @sequenceNumber)
ON CONFLICT ("ContextType") DO UPDATE SET "LastSequenceNumber" = @sequenceNumber
```

**Atomicity trade-off:** The upsert executes before the business transaction commits (auto-commit). If the business transaction subsequently fails, the counter is 1 ahead of actually-published sequences. This is harmless — slaves see a gap, buffer messages, and the gap fills on the next publish. If the gap times out (unlikely for a 1-message gap), the slave triggers a safe resync.

**Sequence exchange during registration:**

| Scenario | Detection | Action |
|----------|-----------|--------|
| Slave reports seq > master's counter | Master restarted with incomplete seed | Full-sync |
| Slave reports empty dictionary | Fresh slave, no replication history | Treated as new instance (existing logic) |
| Slave seq matches master counter | Normal reconnection | No sync needed |

**Sync boundary fix (Part 1 findings):**

| Condition | Before | After | Rationale |
|-----------|--------|-------|-----------|
| `lastRegistered?.AddDays(queueLifetime) ?? DateTimeOffset.Now` | `<` (strict) | `<=` | At exact boundary, RabbitMQ may have already deleted the queue |
| `LastRegistered == null` | Handled by `isNewInstance` | Same | `null?.AddDays(n)` → null; `null <= DateTimeOffset.Now` → false; but `isNewInstance` = true |
| `ForceSync = true` | Bypasses TTL | Same | Confirmed correct: `isNewInstance \|\| isOutOfSync \|\| forceSync \|\| hasSequenceMismatch` |

**Race condition during reconnect (verified safe):**
- `SynchronizationState` blocks `DbChangeSetConsumer` (`await synchronizationState.Ready`)
- During full-sync routing slip, new `DbChangeSet` messages queue up behind the gate
- After sync completes, `ReplicationSequenceTracker.Reset()` clears all context state
- First new message establishes baseline — no messages lost

---

### Test Coverage Summary

| Area | Tests Added | File |
|------|-------------|------|
| Sequence counter | 8 | `ReplicationSequenceCounterTests.cs` |
| Sequence tracker (gap detection, buffer, flush, GetAllLastApplied) | 15 | `ReplicationSequenceTrackerTests.cs` |
| Gap-triggered full-sync in consumer | 5 | `DbChangeSetConsumerTests.GapDetection.cs` |
| Per-entity error isolation | 5 | `DbChangeSetConsumerTests.ErrorIsolation.cs` |
| Replication lag tracker | 4 | `ReplicationLagTrackerTests.cs` |
| Replication lag health check | 4 | `ReplicationLagHealthCheckTests.cs` |
| Interceptor (outbox integration) | 9 | `ChangeTrackingInterceptorTests.cs` |
| Routing slip fault consumer | 5 | `SyncRoutingSlipFaultedConsumerTests.cs` |
| Sync retry state | 4 | `SyncRetryStateTests.cs` |
| Sync retry health check | 3 | `SyncRetryHealthCheckTests.cs` |
| Sync boundary + sequence exchange | 7 | `RegisterInstanceConsumerSyncBoundaryTests.cs` |
| **Total** | **69** | |

## References

- `Core.OS/Persistence/ChangeTrackingInterceptor.cs` — master-side capture + counter persistence
- `Core.OS/Persistence/Consumers/DbChangeSetConsumer.cs` — slave-side apply
- `Core.OS/Persistence/ReplicationSequenceCounter.cs` — master-side sequence assignment (persistent)
- `Core.OS/Persistence/ReplicationSequenceTracker.cs` — slave-side gap detection + reorder buffer
- `Core.OS/Persistence/ReplicationSequenceState.cs` — persisted counter state entity
- `Core.OS/Persistence/ReplicationLagTracker.cs` — slave-side lag measurement
- `Core.OS/DbContext/OutboxDbContext.cs` — Bus Outbox persistence + sequence state table
- `Core.OS/Instance/Commands/RegisterInstance.cs` — registration command with sequence exchange
- `Core.OS/Instance/Initialization/SyncDataActivity.cs` — full-sync path
- `Core.OS/Instance/Consumers/RegisterInstanceConsumer.cs` — registration + sync trigger + mismatch detection
- `Core.OS/Instance/Consumers/SyncRoutingSlipFaultedConsumer.cs` — fault recovery
- `Core.OS/Instance/Services/SyncRetryState.cs` — retry state tracking
- `Core.OS/Instance/HealthCheck/ReplicationLagHealthCheck.cs` — lag health check
- `Core.OS/Instance/HealthCheck/SyncRetryHealthCheck.cs` — sync retry health check
- `Core.OS/Instance/Services/SynchronizationState.cs` — startup gate
- `Core.OS/Modules/Services/ApplicationWorker.cs` — startup seeding + sequence population
- `Core.OS/MessageBus/MassTransit/Configuration/MassTransitConfiguration.cs` — transport config
- `Core.OS/MessageBus/BackEndMediator.cs` — publish path
- `docs/suite-architecture.md` — architectural documentation
- ADR-002 (`docs/ADRs/ADR-002-consumer-idempotency-strategy.md`) — idempotency guarantees
