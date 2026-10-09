---
name: create-consumer
description: Generate a new MassTransit consumer with message contract, implementation, and unit test. Use when creating consumers, message handlers, or command/event processors.
---

# Create a MassTransit consumer

Generate a new MassTransit consumer following ViciOne Suite patterns:

1. Determine if the consumer should be **instance-dependent** or **independent** based on the operation scope
2. Create the message contract where that code base keeps its contracts: `src/Core.Shared` for the suite itself (`src/Core.Shared/AGENTS.md`), the module's `.Public` or `.Internal` project for a module (`AGENTS.md` → *Module Boundaries*)
3. Create the consumer class in the project that owns the state it changes: `src/Core.OS` for most of the suite's own consumers, the module's `.Backend` project for a module
4. Ensure the consumer is **idempotent** (safe to process the same message multiple times)
5. Choose the **error-handling shape** from the message kind and implement it as `AGENTS.md` → *Messaging* requires: ADR-004 D6 for fire-and-forget (`ICommand`, `IInstanceDependentCommand`, `IEvent`), including the fault consumer where the failure has to reach an operator or the UI, and D6a for request/response (`IRequest`, `IInstanceDependentRequest`)
6. Consumers are auto-discovered via assembly scanning (configured in `MassTransitConfiguration.cs`) — no manual registration needed
7. Create a corresponding unit test following `tests/AGENTS.md`. Cover the failure path too: a fire-and-forget consumer must be shown to fault, a request consumer to answer with an `ErrorInfo`.

If the information was not provided, ask for:
- The module name
- The operation description (what the consumer does)
- Whether it modifies instance-local state or global state
