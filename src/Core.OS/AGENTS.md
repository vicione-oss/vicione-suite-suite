# Core.OS

The main application host. Orchestrates module lifecycle, persistence, messaging, authentication, and all infrastructure services.

- **Only project that configures MassTransit** — consumer assembly scanning happens here (`MassTransitConfiguration.cs`)
- All database migrations and EF Core configuration live here
- Module loading via `IModuleHost` — validates dependencies before startup
- Startup sequence: PrepareSuite → AddModules → AddServices → RunCoreOs (see `Program.cs`)
- References Core.Module, Core.Shared, Core.UiHosting, Core.Artifacts

