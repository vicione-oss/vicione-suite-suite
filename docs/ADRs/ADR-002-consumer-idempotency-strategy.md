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
- `UpdateModulePackageOperationsConsumer`: `UnionBy` merge produces same merged result on redelivery.
- `EventForwardToUiConsumer<T>`: pure UI notification relay, no persistent state.
- `GetPasskeysConsumer`: request/response, read-only.
- `RenamePasskeyConsumer`: `IsUpdatedName` check short-circuits when name is already set → publishes success.

#### Acknowledged residual risk: spurious failure events on Create redelivery

`CreateConnectionConsumer` explicitly rejects duplicate IDs with an error event (by design — `UpsertConnectionConsumer` exists for idempotent creation). Under redelivery the "already exists" error is indistinguishable from a genuine duplicate request. Classified as Medium and accepted — same reasoning as `CreateUserConsumer`/`CreateRoleConsumer` in Phase 1b.

#### Acknowledged residual risk: duplicate passkey/login deletion operations

`DeletePasskeysConsumer`: ASP.NET Identity's `RemovePasskeyAsync` is a no-op when the credential is already gone — naturally idempotent. `DeleteExternalLoginConsumer`: `RemoveLoginAsync` may fail on redelivery (login already removed), producing a spurious error event. Classified as Medium — the user-facing impact is a transient error notification that resolves on page refresh.
