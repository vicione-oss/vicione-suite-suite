# Tests

Shared test infrastructure and all test projects for ViciOne Suite.

## Conventions
- Test method names: `snake_case` after a capitalised first word. Two forms are in use, and both are correct — pick one per test class and stay consistent within it:
    - `Should_[tested_behaviour]` — the prevailing form. Use it when the enclosing class already says what is under test, either because the class covers a single member or because tests are grouped in an inner class named after the member (e.g. `RegisterInstanceConsumerTests.ErrorHandling.Should_propagate_exception_when_persistence_is_unavailable`)
    - `[MemberName]_should_[tested_behaviour]` — the older form, without inner classes. Use it when one flat test class covers several members and the method name has to say which (e.g. `SavingChangesAsync_should_publish_added_entity`)
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
