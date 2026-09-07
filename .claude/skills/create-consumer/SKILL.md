---
name: create-consumer
description: Generate a new MassTransit consumer with message contract, implementation, and unit test. Use when creating consumers, message handlers, or command/event processors.
---

# Create a MassTransit consumer

Generate a new MassTransit consumer following ViciOne Suite patterns:

1. Determine if the consumer should be **instance-dependent** or **independent** based on the operation scope
2. Create the message contract in the appropriate `.Public` or `.Internal` project
3. Create the consumer class in the `.Backend` project
4. Ensure the consumer is **idempotent** (safe to process the same message multiple times)
5. Choose the **error-handling shape** — it differs by message kind (ADR-004):
   - **Fire-and-forget** (`ICommand`, `IInstanceDependentCommand`, `IEvent`) — on failure, log **with the exception** and **rethrow** (D6). Never publish an error-shaped completion event from the catch block: `UseInMemoryOutbox` discards everything a throwing consumer published, so publishing there costs the retry and delivers nothing. If the failure has to reach an operator or the UI, generate an `IConsumer<Fault<T>>` alongside it that publishes the correlated error event — trivial (read the fault, publish), no I/O, no state changes, and **no** `[ReadOnlyConsumer]` (it would report once per node).
   - **Request/response** (`IRequest`, `IInstanceDependentRequest`) — the opposite (D6a). Derive from `RequestConsumer<,>` or `InstanceDependentRequestConsumer<,>` and implement `HandleException` to log the exception and return an `ErrorInfo`-carrying response. The response *is* the correlated failure feedback, so do **not** generate a fault consumer for a request.
   - In both cases, never publish a success-shaped completion for failed work (ADR-002).
   - Do not add `UseMessageRetry` to a `ConsumerDefinition<T>` — it nests inside the endpoint ladder and multiplies the attempt count. Retry is configured only in `MassTransitConfiguration.cs`.
6. Consumers are auto-discovered via assembly scanning (configured in `MassTransitConfiguration.cs`) — no manual registration needed
7. Create a corresponding unit test using xUnit, NSubstitute, and AwesomeAssertions. Cover the failure path too: a fire-and-forget consumer must be shown to fault, a request consumer to answer with an `ErrorInfo`.

If the information was not provided, ask for:
- The module name
- The operation description (what the consumer does)
- Whether it modifies instance-local state or global state
