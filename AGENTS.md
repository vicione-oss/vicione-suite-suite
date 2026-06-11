# AGENTS.md — ViciOne Suite

## Project Overview

ViciOne Suite is a modular, distributed automation platform built on .NET 10 and Blazor Server. It provides a flexible, scalable infrastructure for automation workflows — from single edge devices (1.9 GB RAM, ARM Cortex-A53) to multi-site industrial clusters.

**Key concept:** The Suite itself is a platform. It does not implement automation workflows — modules installed on cluster nodes determine the cluster's capabilities.

- **Version:** See `VERSION` file
- **Solution:** `vicione-suite.slnx`
- **Main entry point:** `src/Core.OS/Program.cs`
- **Company:** ifm software
- **License:** See LICENSE.txt

## Architecture at a Glance

```
┌─────────────────────────────────────────────────────────┐
│  ViciOne Suite Instance (Standalone / Master / Slave)   │
├─────────────────────────────────────────────────────────┤
│  Blazor.Server.Backend  │  UI, Identity, API endpoints  │
│  Core.OS                │  Host, persistence, messaging │
│  Core.Module            │  Module loading & lifecycle   │
│  Core.Shared            │  Contracts, events, DTOs      │
│  Core.UiHosting         │  UI host abstractions         │
│  Modules (loaded)       │  Feature extensions           │
└─────────────────────────────────────────────────────────┘
         │                           │
    MassTransit/RabbitMQ         SQLite / PostgreSQL
```

### Instance Modes

| Mode       | Role                                       | Database        |
|------------|--------------------------------------------|-----------------|
| Standalone | Independent, no cluster                    | SQLite          |
| Master     | Central coordinator, single source of truth| PostgreSQL      |
| Slave      | Replicates from master, local operation    | SQLite (replica)|

### Consistency Model

- **Write path (strong consistency):** All global state changes go to the Master via `ICommand` (instance-independent). Slaves forward these to the Master.
- **Read/replication path (eventual consistency):** `ChangeTrackingInterceptor` publishes `DbChangeSet` events via MassTransit. Slaves apply them in FIFO order.
- **Conflict resolution:** Master-wins. Slaves cannot write global state locally.

### Message Interfaces (Sdk.Messaging)

| Interface                    | Scope             | Description                          |
|------------------------------|-------------------|--------------------------------------|
| `ICommand`                   | Global (Master)   | State change directed at the master  |
| `IInstanceDependentCommand`  | Specific node     | State change on a named instance     |
| `IEvent`                     | Fan-out (all)     | Something happened (global)          |
| `IInstanceDependentEvent`    | Specific node     | Something happened (targeted)        |
| `IRequest`                   | Local / Master    | Request/response pattern             |
| `IInstanceDependentRequest`  | Specific node     | Request targeting a named instance   |

## Build & Run

```shell
# Build
dotnet build vicione-suite.slnx

# Run (startup project: Core.OS, profile: Standalone-Ui)
dotnet run --project src/Core.OS

# Tests
dotnet test vicione-suite.slnx
```

Note: After a full repo reset (e.g. `git clean -xfd`) or TypeScript changes, run `npm install && npm run build` first.

**Target framework:** net10.0
**Runtime identifiers:** linux-x64, linux-arm64, win-x64
**Node.js:** Required for font/asset build only (see package.json scripts)

## Code Style & Conventions

- See `docs/code-style-guide.md` for member ordering rules
- Prefer primary constructors over explicit constructors with `private readonly` field assignments
- Test method names: `snake_case` (displayed as sentences in Test Explorer)
- All tests are structured by arrange/act/assert with explicit `// Arrange`, `// Act`, `// Assert` comments
- Frameworks: **xUnit**, **NSubstitute**, **AwesomeAssertions**
- Test runner: Microsoft.Testing.Platform (see global.json)
- Warnings are errors in CI (`TREAT_WARNINGS_AS_ERRORS=true`)
- Languages: en-US primary, de secondary (satellite resources)

## Key Architectural Rules

### Messaging (MassTransit)

- **Consumers MUST be idempotent.** Messages may be redelivered after connectivity loss.
- **Instance-dependent state changes** (anything not in `IModuleDbContext`, e.g. calls to hostmanagement) **MUST** use instance-dependent consumers.
- Commands are fire-and-forget; results are communicated via corresponding Events.
- Slaves only register: instance-dependent consumers + `ReadOnlyConsumer`s.
- Masters/Standalone register: all consumers except `DbChangeset` consumers.

### Module Boundaries

- `ModuleName.Backend` → services, controllers, DB, direct MassTransit usage
- `ModuleName.Client` → Blazor UI only
- `ModuleName.Internal` → shared between Backend and Client of the same module
- `ModuleName.Public` → contracts exposed to other modules (creates a dependency)

### Persistence

- Modules MUST implement both `ISqliteDbContext` and `IPostgresDbContext`.
- Never rely on a specific DB provider — the instance mode determines which is used.
- Bulk EF operations are NOT supported by the `ChangeTrackingInterceptor`.
- Shadow properties and non-JSON-serializable types are not replicated.

### Resource Constraints (Target: Edge-S)

- **1.9 GB RAM, ARM Cortex-A53 (2 cores), 2.3 GB flash storage including OS, limited write cycles**
- Minimize disk writes. SQLite runs in-memory on slaves. Logs go to journald.
- Design with constrained environments as the primary target.

## Design Defaults

- **Always cluster-aware.** Every feature must consider master/slave topology.
- **Offline-first.** Nodes must operate independently during connectivity loss.
- **Module isolation.** Clean SDK boundaries — modules interact via MassTransit and public contracts, not direct references.

## Technology Context

| Technology    | Purpose                                      |
|---------------|----------------------------------------------|
| .NET 10       | Runtime and framework                        |
| Blazor Server | UI (via UiHost module)                       |
| MassTransit   | Async messaging (RabbitMQ or in-memory)      |
| EF Core       | Persistence (SQLite + PostgreSQL)            |
| RabbitMQ      | Distributed message broker                   |
| JFrog         | Module artifact repository                   |
| ASP.NET Identity | Authentication & authorization            |
| TypeScript    | Minimal JS interop (suite.js)                |

### Active Migrations

- **DevExpress UI → custom Blazor components** (from `blazor-components` repo)
- **Integration & UI testing strategy** — being defined and implemented
- **Stabilizing master-slave architecture** — current primary SDK focus

## Product Vision

ViciOne Suite aims to be a robust, scalable platform easily adapted to brownfield industrial environments. The solution must be primarily maintainable by coding agents and the open source community. All development must remain well-documented and purposeful to ensure trust in the software.

## Semantic Versioning

The Suite SDK follows semantic versioning strictly. Any change to SDK contracts must respect semver guarantees. Breaking changes require a major version bump.

## Skills

Reusable skill definitions located in `.claude/skills/` following the [Agent Skills](https://agentskills.io/) open standard. Supported by GitHub Copilot and Claude Code.

| Skill | Description |
|-------|-------------|
| [create-module](.claude/skills/create-module/SKILL.md) | Scaffold a new module with Backend/Client/Internal/Public projects |
| [create-consumer](.claude/skills/create-consumer/SKILL.md) | Generate a MassTransit consumer with contract, implementation, and test |
| [pre-mr-review](.claude/skills/pre-mr-review/SKILL.md) | Pre-MR checklist: translations, code style, idempotency, changelog |
| [review-module](.claude/skills/review-module/SKILL.md) | Architecture compliance audit for a module |
| [build](.claude/skills/build/SKILL.md) | Build the solution |
| [test](.claude/skills/test/SKILL.md) | Run the test suite and analyze failures |

## Project Structure

Per-directory `AGENTS.md` files exist in key projects with project-specific constraints:
- `src/Core.OS/AGENTS.md` — host app, only project configuring MassTransit
- `src/Core.Module/AGENTS.md` — module loading and assembly isolation
- `src/Core.Shared/AGENTS.md` — contracts only, no implementation
- `src/Blazor.Server.Backend/AGENTS.md` — UI host, no business logic
- `tests/AGENTS.md` — test conventions and shared fixtures

## Related Repositories

`suite-sdk` · `blazor-components` · `cluster-mgmt` · `cluster-editor` · `functionblocks` · `hostmgmt` · `dx` · `ping` · `data-collection-wizard` · `oee`
