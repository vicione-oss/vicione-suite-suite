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
5. Consumers are auto-discovered via assembly scanning (configured in `MassTransitConfiguration.cs`) — no manual registration needed
6. Create a corresponding unit test using xUnit, NSubstitute, and AwesomeAssertions

If the information was not provided, ask for:
- The module name
- The operation description (what the consumer does)
- Whether it modifies instance-local state or global state
