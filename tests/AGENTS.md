# Tests

Shared test infrastructure and all test projects for ViciOne Suite. Applies in addition to `../AGENTS.md`.

Longer explanations with examples: `docs/test-structure-guide.md` (structure of a test class),
`docs/integration-testing.md` (integration tests), `docs/e2e-testing.md` (Playwright E2E tests).

## Conventions
- Test method names: `Should_[tested_behaviour]` in `snake_case` after the capitalised first word, displayed as sentences in Test Explorer. The enclosing class says what is under test: either the class covers a single member, or the tests are grouped in an inner class named after the member (e.g. `RegisterInstanceConsumerTests.ErrorHandling.Should_propagate_exception_when_persistence_is_unavailable`)
- Frameworks: xUnit, NSubstitute, AwesomeAssertions, bUnit
- Test runner: Microsoft.Testing.Platform (see `global.json`)

## Arrange / Act / Assert
- `// Arrange`, `// Act`, `// Assert` structure every test, in that order, and stay verbatim. They are structure
  markers, not sentences, so the comment form rule does not apply to them.
- `// Act + Assert` replaces the two markers when the action *is* the assertion, e.g.
  `await act.Should().ThrowAsync<InvalidOperationException>();`.
- `// Arrange` may be left out when the body has no arrange code at all (inputs come from data attributes or
  fields). Any arrange code in the body requires it — `[Fact]` and `[Theory]` alike.
- Any further comment only when the *setup* is counter-intuitive; the test name carries the intent.

## XML docs
- `CS1591` is suppressed in `*Tests` projects; the test name carries the intent. Helper members still take `///`.

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
