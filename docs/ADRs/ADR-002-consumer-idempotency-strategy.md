# ADR-002: Consumer Idempotency Strategy

## Status

Accepted

## Date

2026-05-27

## Context

Messages may be redelivered after connectivity loss. The Phase 1a audit found two non-idempotent consumers:

| Consumer | Risk | Issue |
|----------|------|-------|
| `DbChangeSetConsumer` | Critical | `Add` causes `DbUpdateException` on redelivery; exception caught and swallowed, silently dropping remaining batch changes. |
| `ControlInstanceConsumer` (Delete) | High | Redelivery after deletion returns without publishing `ControlInstanceCompleted`, leaving orchestrators hanging. |

**Constraints:** Edge-S target (1.9 GB RAM, ARM Cortex-A53, 2.3 GB flash including OS, limited write cycles), SQLite on slaves, no Redis/external stores viable, sequential processing already enforced (PrefetchCount=1).

## Decision

**Natural idempotency via upsert semantics.**

### `DbChangeSetConsumer`

- `Added`/`Modified`: FindAsync by PK → update if exists, add if not (upsert).
- `Deleted`: FindAsync by PK → remove if exists, no-op if not.
- Remove blanket `catch (DbUpdateException)` — let failures propagate to MassTransit retry, then dead-letter.

### `ControlInstanceConsumer` (Delete)

Publish `ControlInstanceCompleted` even when the instance is already gone, so orchestrators are never stuck.

## Alternatives Rejected

| Option | Why rejected |
|--------|-------------|
| Idempotency key table | Requires durable deduplication storage — adds write amplification on flash, cleanup policy complexity |
| Optimistic concurrency (version column) | Schema changes across all module DbContexts, breaks SDK contract |
| MassTransit deduplication middleware | Loses state on restart, doesn't address partial batch failures |

## Consequences

- Per-entity `FindAsync` before insert adds sub-millisecond overhead (in-memory SQLite PK lookup)
- Genuine `DbUpdateException` from schema mismatches now dead-letters instead of being swallowed (correct behavior — operators should be alerted)
- Out-of-order delivery can still cause stale overwrites in rare reconnection scenarios (mitigated by PrefetchCount=1 and FIFO queues)

## Compliance

- New persistence consumers must demonstrate idempotency in the MR description

## Amendments

### 2026-05-28 — Phase 1b: User Management & Identity

Audit of `Core.OS/UserManagement/Consumers/` extended the pattern catalogue with two additional concerns.

#### Pattern: completion-on-already-deleted (extends the `ControlInstanceConsumer` rule)

`DeleteUserConsumer` and `DeleteRoleConsumer` previously published a `*DeletedEvent` with `DeleteFailedNotFound` error when the target was missing. Under redelivery this produces a spurious failure event after the original success has already completed. Both consumers now publish the success-shaped `*DeletedEvent` (no `ErrorInfo`) when the entity is already gone, matching the `ControlInstanceConsumer` (Delete) fix.

#### Pattern: do not swallow exceptions in mail consumers

`SendResetPasswordLinkConsumer` and `SendVerifyEmailAddressLinkConsumer` previously caught and logged every exception, hiding SMTP outages and template-rendering bugs from operators and preventing MassTransit retry. Both now let exceptions propagate to the MassTransit pipeline. Accepted trade-off: redelivery after a successful `SendMail` whose ack was lost can produce a duplicate email — recipients use the first valid link, so this is preferable to silently dropping notifications.

#### Acknowledged residual risk: spurious failure events on Create/Update redelivery

`CreateUserConsumer`, `CreateRoleConsumer`, `UpdateUserConsumer` (password-change branch only) all publish a domain-specific failure event when redelivery encounters state that the first delivery already changed (user/role exists; current password no longer matches). Without an idempotency-key store these are indistinguishable from a genuine duplicate request. Classified as Medium and **not fixed in this phase**: the failure event is observationally identical to a duplicate user-initiated submission, and UI layers already pre-check name uniqueness. Revisit if/when a deduplication store is introduced (see "Alternatives Rejected").

### 2026-06-02 — Phase 1c: Instance, Cluster & Host Management

Audit of `Core.OS/Instance/Consumers/` and `Core.OS/HostManagement/Consumers/` (33 consumers total).

#### Pattern: completion-on-already-deleted (applied to `DeleteArtifactRepositoryConsumer`)

Same pattern as Phase 1b `DeleteUserConsumer`/`DeleteRoleConsumer`. `DeleteArtifactRepositoryConsumer` previously threw `InvalidOperationException` when the repository was already gone, which the catch block converted into a spurious error event. Now publishes success-shaped `ArtifactRepositoryChanged(CrudAction.Deleted)` when the repository is not found — downstream cache-invalidation consumer handles this identically.

#### Pattern: do not swallow exceptions (applied to `RegisterInstanceConsumer`)

`RegisterInstanceConsumer` caught all exceptions and only logged them, preventing MassTransit from retrying transient failures (DB timeouts, connectivity blips). Now rethrows after logging, allowing the MassTransit retry pipeline to handle transient errors. The consumer's operations (upsert + routing slip) are naturally idempotent, making retry safe.

#### Acknowledged residual risk: duplicate side effects on terminal operations

Host management consumers (`ControlServiceConsumer`, `ControlSystemConsumer`, `InstallSuiteVersionConsumer`, `UpdateSystemConsumer`) execute host-level commands (service restart, system shutdown, package install) that are terminal — the process is killed during or after execution, preventing redelivery. In the unlikely event redelivery occurs before the terminal action completes, re-executing the same command produces the same outcome. Classified as Medium and accepted without fix.

#### Acknowledged residual risk: duplicate backup files

`CreateBackupConsumer` creates a new backup file (with timestamp-based name) on each invocation. Redelivery produces a duplicate backup on flash storage. This wastes write cycles on Edge-S but causes no data corruption or operational confusion. Classified as Medium and accepted — fixing would require an idempotency key store.

### 2026-06-03 — Phase 1d: Connections, Modules & Remaining Consumers

Audit of `Core.OS/Connections/Consumers/`, `Core.OS/Modules/Consumers/`, `Core.OS/MessageBus/MassTransit/`, and `Blazor.Server.Backend/` (12 consumers total).

#### Pattern: completion-on-already-deleted (applied to `DeleteConnectionConsumer` and `DeleteTagConsumer`)

Same pattern as Phase 1b/1c. Both consumers previously published an error-shaped event (with `ErrorInfo`) when the target entity was not found. Under redelivery after successful deletion this produces a spurious failure event. Both now publish success-shaped completion events when the entity is already gone.

#### Naturally idempotent consumers (no fix required)

- `UpsertConnectionConsumer`: upsert pattern (find-by-PK → add or update).
- `UpsertTagConsumer`: upsert pattern (find-by-PK → add or update).
- `TestConnectionConsumer`: stateless read-only test with result event.
- `UpdateModuleOptionsConsumer`: file overwrite (`FileMode.Create`) produces same result on redelivery.
- `UpdateModulePackageOperationsConsumer`: re-dispatching the same instance-dependent `EnqueueModulePackageOperations` commands to every instance produces the same fan-out on redelivery.
- `EnqueueModulePackageOperationsConsumer`: `UnionBy` merge in the operation store produces the same merged result on redelivery.
- `EventForwardToUiConsumer<T>`: pure UI notification relay, no persistent state.
- `GetPasskeysConsumer`: request/response, read-only.
- `RenamePasskeyConsumer`: `IsUpdatedName` check short-circuits when name is already set → publishes success.

#### Acknowledged residual risk: spurious failure events on Create redelivery

`CreateConnectionConsumer` explicitly rejects duplicate IDs with an error event (by design — `UpsertConnectionConsumer` exists for idempotent creation). Under redelivery the "already exists" error is indistinguishable from a genuine duplicate request. Classified as Medium and accepted — same reasoning as `CreateUserConsumer`/`CreateRoleConsumer` in Phase 1b.

#### Acknowledged residual risk: duplicate passkey/login deletion operations

`DeletePasskeysConsumer`: ASP.NET Identity's `RemovePasskeyAsync` is a no-op when the credential is already gone — naturally idempotent. `DeleteExternalLoginConsumer`: `RemoveLoginAsync` may fail on redelivery (login already removed), producing a spurious error event. Classified as Medium — the user-facing impact is a transient error notification that resolves on page refresh.

### 2026-08-03 — Phase 1e: Consumer Inventory Refresh

Re-audit after the consumer count grew from 59 to 65. Six consumers were added since Phase 1d and are classified here.

#### Naturally idempotent consumers (no fix required)

- `ReconcileModuleManifestConsumer`: diffs the desired package set against the locally installed one, so a redelivered manifest produces an empty diff once reconciliation succeeded. The restart-loop guard signature file is written **after** the enqueue and restart dispatch — deliberately, so a transient failure is retried instead of being suppressed by the guard.
- `SyncRoutingSlipFaultedConsumer`: guards on the local instance id, resets `SynchronizationState`, and delegates attempt counting to `SyncRetryState`. Redelivery of the same fault consumes one retry budget slot but converges to the same terminal state (re-registration or degraded). The delayed re-registration send runs on a detached task whose body catches and logs every exception, so no fault is lost.
- `GetConnectionsConsumer`, `GetTagsConsumer`: read-only request/response.

#### Acknowledged residual risk: duplicate cluster-wide restarts

`RestartAllInstancesConsumer` fans an instance-dependent `ControlInstance(Restart)` out to every registered instance. Redelivery re-sends the restart to nodes that already restarted. The target `ControlInstanceConsumer` is itself idempotent and the restart is terminal, so the worst case is one extra reboot cycle. Classified as Medium and accepted — deduplicating would require an idempotency key store (see "Alternatives Rejected").

#### Pattern: never publish a success-shaped completion for failed work

`UpdateModuleOptionsConsumer` was idempotent (file overwrite) but published a bare `ModuleOptionsChanged(moduleId)` from its catch block. The client (`ModuleManagementService`) completes the pending command with **success** for any `ModuleOptionsChanged` without an `Error`, so a failed store was reported to the operator as applied. It now publishes `ModuleOptionsChanged(moduleId, ErrorInfo(ModuleErrorCodes.UpdateOptionsFailed, …))`, which the client already routes to `CompleteWithError`.

This generalises the Phase 1b/1c rules: a completion event may be *success-shaped* only when the desired state actually holds — either because the work succeeded, or because it was already done (completion-on-already-deleted).

#### Defect: exception dropped from the enqueue error log

`EnqueueModulePackageOperationsConsumer` logged its failure through a `[LoggerMessage]` overload without an `Exception` parameter, discarding the stack trace of every enqueue failure. The overload now takes the exception, and the magic error code `230` was replaced by `ModuleErrorCodes.EnqueueOperationsFailed`.

At the time this was written the consumer still reported the failure via `ModulePackageOperationsEnqueued`/`ModulePackageOperationsChanged` instead of rethrowing, because `UseInMemoryOutbox` buffers everything published inside a consumer and discards it when the consumer throws — so "publish the error event **and** rethrow" would have delivered nothing at all. Choosing between retry and correlated operator feedback was deferred to Phase 3a.

**Resolved by ADR-004 (D6).** The consumer now logs with the exception and rethrows, so the enqueue rides the retry ladder and finally dead-letters, and the correlated feedback is published by `EnqueueModulePackageOperationsFaultConsumer` from the fault pipe — which runs outside the faulted consumer's outbox scope, so it survives. The two are no longer mutually exclusive.

#### Compliance backfill

Explicit redelivery tests were added for the ADR-002 fixes that had none, so every documented pattern is now covered by at least one test:

| Pattern | Consumer | Test |
|---------|----------|------|
| Upsert semantics | `DbChangeSetConsumer` | `DbChangeSetConsumerTests.Idempotency` |
| Completion-on-already-deleted | `ControlInstanceConsumer` (Delete) | `Should_publish_completion_when_instance_already_deleted` |
| Completion-on-already-deleted | `DeleteArtifactRepositoryConsumer` | `Should_publish_success_event_when_repository_already_deleted` |
| Completion-on-already-deleted | `DeleteTagConsumer` | `Should_publish_success_event_for_unknown_tag_to_ensure_idempotency` |
| Do not swallow exceptions | `RegisterInstanceConsumer` | `Should_propagate_exception_when_persistence_is_unavailable` |
| Never report failure as success | `UpdateModuleOptionsConsumer` | `Should_send_event_with_error_if_store_fails` |
