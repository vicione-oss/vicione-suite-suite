# Tests

Shared test infrastructure and all test projects for ViciOne Suite.

## Conventions
- Test method names: `Should_[tested_behaviour]` in `snake_case` (e.g. `Should_publish_change_event_for_new_connection`). The method name is reflected by the inner test class
- All tests structured with `// Arrange`, `// Act`, `// Assert` comments
- Frameworks: xUnit, NSubstitute, AwesomeAssertions, bUnit

## Test Utilities
- **Core.Tests.Tools** — system test settings, artifact repo config, path helpers, trait constants. Use these; don't create ad-hoc config.
- **Blazor.Tests.Tools** — bUnit context setup, centralized JSInterop mocking, pre-configured service containers. Use `TestContextExtensions` for Blazor component tests.

## Test Modules
- `TestModule.Backend` / `TestModule.Client` — minimal modules for integration tests
- `TestSystem.Backend` — system-level test module
- `TestUiHost.Backend` — test UI host

## Rules
- Always use shared fixtures from Tests.Tools projects
- Never mock what can be tested with the in-memory MassTransit harness
- Test projects must not reference other test projects' internal helpers directly
