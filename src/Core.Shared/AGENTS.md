# Core.Shared

Shared contracts, events, commands, DTOs, and configuration models used across all projects. Applies in addition to `../../AGENTS.md`.

- **No implementation** — only interfaces, data classes, records, and enums
- Contains all MassTransit message contracts (events, commands, requests) in domain-specific namespaces
- Heavily referenced by Core.OS, Blazor.Server.Backend, and all modules
- Uses StronglyTypedId source generator for type-safe IDs
- **No internal project dependencies** — this is the bottom of the dependency graph

