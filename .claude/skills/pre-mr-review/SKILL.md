---
name: pre-mr-review
description: Run pre-MR review checks including translations, code style, consumer idempotency, consumer error handling, and changelog validation. Use before raising a merge request or when reviewing branch changes.
---

# Pre-MR Review

Run this after implementing changes on a branch to catch common issues before raising an MR.

---

## 1. Missing translations

For every `.resx` file changed on the branch:

1. Find all changed resx files: `git diff main...HEAD --name-only | grep '\.resx$'`
2. For each changed file, identify its sibling language files (e.g. `Foo.resx` → `Foo.de.resx`)
3. Extract all `name=` keys from the default (English) file and each language file
4. Report any key present in the default file but missing from a language file, or vice versa

---

## 2. Naming and code style consistency

For every `.cs` and `.razor` file changed on the branch, check:

**Namespace placement**
- Types implementing `IEvent` must live in an `Events` namespace and under an `Events/` folder
- Types implementing `ICommand` or `IInstanceDependentCommand` must live in a `Commands` namespace and under a `Commands/` folder
- Types implementing `IRequest` or `IInstanceDependentRequest` must live in a `Requests` namespace and under a `Requests/` folder
- Multiple unrelated types must not be bundled in the same file

**Member ordering** (within each type, groups separated by a blank line)
- Constants → Static fields → Fields → Properties → Events → Constructors → Methods

**Naming conventions**
- Public members: `PascalCase`
- Private fields: `_camelCase`
- Test methods: capital first letter, `snake_case` rest. Two forms are correct — flag only inconsistency *within* a test class, never the choice between them:
    - `Should_[tested_behaviour]` when the enclosing class or an inner class named after the member already says what is under test (e.g. `RegisterInstanceConsumerTests.ErrorHandling.Should_propagate_exception_when_persistence_is_unavailable`). This is the prevailing form
    - `[MemberName]_should_[tested_behaviour]` when one flat class covers several members (e.g. `SavingChangesAsync_should_publish_added_entity`)

**Constructor style**
- Prefer primary constructors over explicit constructors with `private readonly` field assignments

**Attribute formatting**
- Each attribute on its own line — never combined as `[A, B]` or stacked as `[A][B]` on a single line

**Test structure**
- All tests must have `// Arrange`, `// Act`, `// Assert` comments

---

## 3. Consumer idempotency

For every new or modified MassTransit consumer on the branch:
- Verify the consumer handles duplicate messages safely
- Instance-local state changes (outside IModuleDbContext) must use IInstanceDependentCommand consumers

---

## 4. Consumer error handling (ADR-004)

For every new or modified consumer on the branch, check the rules for its message kind — they are opposites, so identify the kind first.

**Fire-and-forget consumers** (`ICommand`, `IInstanceDependentCommand`, `IEvent`) — D6:
- A catch block must not publish an error-shaped completion event *instead of* rethrowing. It must log with the exception and rethrow, so the retry ladder applies and the message finally dead-letters. (`UseInMemoryOutbox` discards anything a throwing consumer published, so publishing from the catch and rethrowing delivers nothing.)
- If the failure has to reach an operator or the UI, the branch must also add an `IConsumer<Fault<T>>` publishing the correlated error event.
- Fault consumers must be trivial (read the fault, publish the event — no I/O, no state changes) and must **not** carry `[ReadOnlyConsumer]`, which would report once per node.
- If the command is **fanned out per instance** (one copy per node under one correlation id), the fault consumer must gate its correlated, UI-facing report on `command.InstanceId` being the local instance. Otherwise it emits one report per failing node and races the local node's own verdict for the same correlation id. Per-node facts belong on a separate, non-UI event that carries the instance id.

**Request consumers** (`IRequest`, `IInstanceDependentRequest`) — D6a:
- Must answer, not throw: catch, log with the exception, respond with an `ErrorInfo`-carrying response. Deriving from `RequestConsumer<,>` / `InstanceDependentRequestConsumer<,>` provides this; a raw `IConsumer<TRequest>` must reproduce it by hand.
- Must **not** ship an `IConsumer<Fault<T>>`.

**Both kinds:**
- Never publish a success-shaped completion for failed work — a completion event emitted from a catch block must carry an `ErrorInfo` (ADR-002).
- An exception being logged must be passed to the `[LoggerMessage]` method as an `Exception` parameter, not flattened into the message text — otherwise the stack trace is lost.
- A failure log on a message-handling path must also carry the message's correlation id, and its instance id where the message is instance-dependent. On a standalone instance the faulted message itself is discarded, so that line is the only post-mortem evidence, and without the ids it cannot be tied to the operation that produced it. Check it against the consumer's own success-path logs: if those carry the correlation id and the error path does not, that is the defect.
- Error codes come from the module's `*ErrorCodes` constants, never a magic number.
- No `UseMessageRetry` inside a `ConsumerDefinition<T>` — it nests inside the endpoint ladder and multiplies the attempt count. Retry, redelivery, outbox, kill-switch and error-queue behaviour are configured only in `MassTransitConfiguration.cs`.
- No consume filter that must run *per delivery attempt* — `UseMessageScope`, `UseMessageLifetimeScope`, the in-memory outbox — may be configured on the bus. A bus-level filter wraps the endpoint-level ones, so it would sit outside the retry filter and every attempt would share it. They belong in the endpoint callback, after `UseMessageRetry`.

---

## 5. Changelog

Check that every change a user, operator or module developer can notice has an entry in `CHANGELOG.md`, and that each
entry follows the *Changelog* rules in `AGENTS.md`: under the unreleased header, one line from the reader's view, no
implementation details. Report entries that explain a cause or a mechanism.

---

## 6. Run tests

Run the unit tests for all projects that own changed files:

1. Identify test projects that exercise the changed source files
2. Run those test projects
3. Report any failures — do not mark the review as complete until all tests pass

---

## Output

Report **only actual issues found.** For each issue state: file path, line number, and a one-line description of the violation. If nothing is found, say so.
