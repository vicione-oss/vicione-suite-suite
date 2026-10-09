---
name: create-module
description: Scaffold a new ViciOne Suite module with Backend, Client, Internal, and Public projects. Use when creating new modules or extending the platform with new capabilities.
---

# Scaffold a new ViciOne Suite module

Create the project structure for a new module following ViciOne Suite conventions:

## Required projects:

- `ModuleName.Backend` — Services, controllers, DB contexts, consumers
- `ModuleName.Client` — Blazor UI components and pages
- `ModuleName.Internal` — Shared code between Backend and Client
- `ModuleName.Public` — Public contracts for other modules (if needed)

## Each project must:

- Target net10.0
- Follow the naming convention: `ViciOne.Suite.<ModuleName>.<Suffix>`
- Backend: Implement `BackendModule` base class with proper `ModuleId`
- Client: Implement `ClientModule` base class with proper `ModuleId`
- Backend persistence: Use `[ModuleDbContext]` attribute — source generator produces SQLite/Postgres subclasses
- Include proper module-metadata.json

## Testing:

- Create `ModuleName.Tests` project following `tests/AGENTS.md`

## Reference

The `addon-project-templates` repository contains a Visual Studio template that scaffolds a module solution with the necessary content. Use it as the canonical reference for project structure and boilerplate.

Ask for the module name and its primary responsibility before scaffolding.
