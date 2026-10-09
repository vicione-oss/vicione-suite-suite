# Test Structure Guide

## 1. File & Namespace

- **Rule 1.1** One top-level test class per file; file name matches the class
  (`ModuleHostBuilderTests.cs` → `ModuleHostBuilderTests`).
- **Rule 1.2** Use file-scoped namespaces that mirror the folder path
  (`namespace Core.OS.Tests.Instance.Extensions;`).
- **Rule 1.3** Name the top-level class `[SubjectUnderTest]Tests`, where the
  subject is the type/extension/consumer under test.

## 2. Outer Fixture Class (shared state)

- **Rule 2.1** The outer class owns all shared state as `private readonly`
  fields: substitutes, options, `MockFileSystem`, `ServiceProvider`, and reusable
  test data (e.g. `_releaseSource`, `_stagingSource`, `_instanceOptions`).
- **Rule 2.2** Do setup in the constructor or field initializers — never in
  `[Fact]` bodies if it is shared by more than one test.
- **Rule 2.3** Put reusable setup logic in **private helper methods**. Use
  `static` helpers when they don't touch instance state
  (`SetupRecoveryStateFile`, `SetupRecoveryStateFolder`) and instance helpers
  when they mutate fixture fields (`SetupBackendModuleMetadataJson`).
- **Rule 2.4** For DI-based subjects, build the `ServiceProvider` once in the
  constructor and resolve per test with `GetRequiredService<T>()`.

## 3. Group Tests in Nested Classes (one per method/behavior)

- **Rule 3.1** Create **one nested class per method or logical behavior** of the
  subject. Name it **exactly** after the method under test: `Build`,
  `WithOptionsSupport`, `CreateOrUpdate`, `Delete`, `GetRepositories`,
  `EnsureInstanceIdFile`, `UseRecoveryMode`.
- **Rule 3.2** Nested classes **inherit the outer class** to reuse the fixture:
  `public sealed class Build : ModuleHostBuilderTests`.
- **Rule 3.3** Mark leaf nested classes `sealed`.
- **Rule 3.4** A test class with a single behavior (e.g. one consumer) may stay
  **flat** — no nesting — as in `ArtifactRepositoryChangeConsumerTests`.

## 4. Test Method Naming (`snake_case`)

- **Rule 4.1 — Nested style (preferred):** the nested class supplies the method
  prefix, so the test method uses `Should_[behavior]`:
  `Build.Should_build_host_without_features`,
  `UseRecoveryMode.Should_return_continue_on_first_startup`.
- **Rule 4.2 — Flat style:** when not nested, test method names are still
  `Should_[behavior]`; the class name provides the subject context.
- **Rule 4.3** Describe **behavior/outcome**, not implementation. No `Test_`
  prefixes, no numbering.

## 5. Arrange / Act / Assert

- **Rule 5.1** Every test has explicit `// Arrange`, `// Act`, `// Assert`
  comments, in that order. `// Arrange` may be left out when the body has no
  arrange code at all (inputs come from data attributes or fields). Any arrange
  code in the body requires it, for `[Fact]` and `[Theory]` alike.
- **Rule 5.2** Collapse to `// Act + Assert` only when the action *is* the
  assertion (exception checks, MassTransit consume):
  `await act.Should().ThrowAsync<InvalidOperationException>();`.
- **Rule 5.3** Pending tests use `[Fact(Skip = "reason")]` and keep the empty
  `// Arrange` / `// Act` / `// Assert` scaffold.

```csharp
[Theory]
[InlineData("Debug", "debug")]
public void Should_transform_pascal_case_to_kebab_case(string value, string expected)
{
    // Act
    var result = _transformer.TransformOutbound(value);

    // Assert
    result.Should().Be(expected);
}
```

## 6. Assertions

- **Rule 6.1** Use AwesomeAssertions fluent style: `.Should().Be(...)`,
  `.Should().BeTrue()`, `.Should().BeEmpty()`, `.Should().HaveCount(n)`,
  `.Should().ContainSingle(...)`, `.Should().BeEquivalentTo(...)`.
- **Rule 6.2** Use `BeEquivalentTo` for object-graph comparison and `Be` for
  scalars.
- **Rule 6.3** Prefer fluent assertions over raw `Assert.*`. The only accepted
  `Assert.*` use is null-guarding for flow analysis (`Assert.NotNull(state);`)
  before dereferencing.
- **Rule 6.4** Add a reason string when it clarifies intent
  (`.Should().HaveCount(2, "Test.Backend|Client")`).

## 7. Test Doubles & Fakes

- **Rule 7.1** Prefer real in-memory fakes over mocks: `MockFileSystem` for I/O,
  the MassTransit test harness for messaging. **Never mock what the in-memory
  MassTransit harness can verify** (`tests/AGENTS.md`).
- **Rule 7.2** Use `Substitute.For<T>()` for collaborators, `Arg.Any<T>()` for
  argument matching, and verify with `.Received(1)` / `.DidNotReceive()`.
- **Rule 7.3** Use shared tooling — `TestConfig`, `TestFactory`,
  `MassTransitTester` — from `Core.Tests.Tools` / `Sdk.Testing.Backend`. Do not
  hand-roll ad-hoc configuration.

## 8. Async & Cancellation

- **Rule 8.1** Async tests are `async Task` (never `async void`).
- **Rule 8.2** Always pass `TestContext.Current.CancellationToken` to async calls
  under test.

## 9. Consumer Tests (MassTransit)

- **Rule 9.1** Register the consumer and its substituted dependencies in a
  single `Action<IBusRegistrationConfigurator>` field (`_configureServices`).
- **Rule 9.2** Drive the harness with
  `tester.TestEvent<TEvent, TConsumer>(@event)` and verify side effects on the
  substituted collaborators.
- **Rule 9.3** Test methods **must start with `Should_`**. The class name
  already provides the subject context (e.g. `DeleteTagConsumerTests`), so the
  consumer or command name must not be repeated as a method prefix.
  Use `Should_remove_tag` not `DeleteTag_should_remove_tag`.

---

## Canonical Skeleton

```
public class ModuleHostBuilderTests            // 1. outer fixture
{
    private readonly MockFileSystem _fileSystem = new();   // 2. shared state

    private ModuleMetadata SetupBackendModuleMetadataJson(/* ... */) // 2.3 helper
    {
        /* ... */
    }

    public sealed class Build : ModuleHostBuilderTests     // 3. group per method
    {
        [Fact]
        public async Task Should_build_host_without_features()   // 4.1 naming
        {
            // Arrange
            var hostBuilder = /* ... */;

            // Act
            var moduleHost = await hostBuilder.Build(
                () => null, TestContext.Current.CancellationToken);  // 8.2

            // Assert
            moduleHost.GetModules().Should().BeEmpty();              // 6.1
        }
    }
}
```

---

## Refactoring Checklist

When unifying an existing test class, verify each item:

- [ ] Top-level class named `[Subject]Tests`, file-scoped namespace mirrors path.
- [ ] All shared state moved to `private readonly` fields on the outer class.
- [ ] Shared setup moved to the constructor / field initializers.
- [ ] Reusable setup extracted into `private` (`static` where possible) helpers.
- [ ] Tests grouped into nested classes named after the method under test,
      each inheriting the outer class and marked `sealed`
      (or intentionally kept flat for single-behavior classes).
- [ ] Every test method starts with `Should_`.
- [ ] Every test has `// Arrange` / `// Act` / `// Assert` (or `// Act + Assert`;
      `// Arrange` omitted when there is no arrange code).
- [ ] Assertions use AwesomeAssertions; raw `Assert.*` only for null-guards.
- [ ] `MockFileSystem` / MassTransit harness used instead of mocking I/O or bus.
- [ ] Async tests are `async Task` and pass
      `TestContext.Current.CancellationToken`.
- [ ] Shared tooling (`TestConfig`, `TestFactory`, `MassTransitTester`) used
      instead of ad-hoc config.
