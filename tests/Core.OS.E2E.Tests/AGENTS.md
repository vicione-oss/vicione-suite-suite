# AGENTS.md — Core.OS.E2E.Tests

Black-box Playwright UI smoke tests that drive a real browser against a running Suite instance.
General test conventions live in `../AGENTS.md`; project setup, isolation, tagging, running and
debugging are documented on `E2ETest` and in `docs/e2e-testing.md` — read those rather than
re-deriving them from the tests. This file only covers writing conventions that live nowhere else.

## Deviations from the general test conventions
- Test method names describe the behaviour directly (e.g. `Seeded_user_can_log_in`), without the
  `Should_` prefix `../AGENTS.md` otherwise requires.
- `// Arrange` / `// Act` / `// Assert` comments are merged or omitted where a step is trivial or
  there's nothing to arrange or where they do not fit the theme of the test in question; prefer a short comment explaining *why* over restating the step name.

## Locators & assertions
- Prefer resilient, user-facing locators (role/label/text, or `name=` attributes that are required for model binding and therefore stable). Avoid CSS/XPath tied to layout or styling.
- Locators are strict by default: a single-element action (e.g. `ClickAsync`) throws when more than one element matches. Disambiguate by refining the locator or chaining `.Filter(new() { HasText = ... })` / `.GetByRole(...)`, not with positional `.Nth()`/`.First()`/`.Last()`.
- Assert with Playwright's web-first `Expect(...)`; it auto-retries until the condition holds or times out.
- Never wait manually (`Task.Delay`, fixed sleeps, arbitrary timeouts). For eventually-consistent state (e.g. replication), use a bounded "eventually" retry, not a fixed wait.
- Reference: [Playwright locators guide](https://playwright.dev/dotnet/docs/locators).

## Page objects
- Selectors and page actions belong in `Pages/` classes; test methods express intent only, so a markup change is a one-place fix.
- Add a new interaction to the relevant page object rather than inline in a test.
