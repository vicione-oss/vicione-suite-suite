---
name: pre-mr-review
description: Run pre-MR review checks including translations, code style, consumer idempotency, and changelog validation. Use before raising a merge request or when reviewing branch changes.
---

# Pre-MR Review

Run this after implementing changes on a branch to catch common issues before raising an MR.

---

## 1. Missing translations

For every `.resx` file changed on the branch:

1. Find all changed resx files: `git diff main...HEAD --name-only | grep '\.resx$'`
2. For each changed file, identify its sibling language files (e.g. `Foo.resx` → `Foo.de.resx`)
3. Extract all `name=` keys from the default (English) file and each language file
4. Report any key present in the default file but missing from a language file, or vice versa

---

## 2. Naming and code style consistency

For every `.cs` and `.razor` file changed on the branch, check:

**Namespace placement**
- Types implementing `IEvent` must live in an `Events` namespace and under an `Events/` folder
- Types implementing `ICommand` or `IInstanceDependentCommand` must live in a `Commands` namespace and under a `Commands/` folder
- Types implementing `IRequest` or `IInstanceDependentRequest` must live in a `Requests` namespace and under a `Requests/` folder
- Multiple unrelated types must not be bundled in the same file

**Member ordering** (within each type, groups separated by a blank line)
- Constants → Static fields → Fields → Properties → Events → Constructors → Methods

**Naming conventions**
- Public members: `PascalCase`
- Private fields: `_camelCase`
- Test methods: `[MethodName]_should_[tested_behaviour]` — capital first letter, snake_case rest (e.g. `SavingChangesAsync_should_publish_added_entity`)

**Constructor style**
- Prefer primary constructors over explicit constructors with `private readonly` field assignments

**Attribute formatting**
- Each attribute on its own line — never combined as `[A, B]` or stacked as `[A][B]` on a single line

**Test structure**
- All tests must have `// Arrange`, `// Act`, `// Assert` comments

---

## 3. Consumer idempotency

For every new or modified MassTransit consumer on the branch:
- Verify the consumer handles duplicate messages safely
- Instance-local state changes (outside IModuleDbContext) must use IInstanceDependentCommand consumers

---

## 4. Changelog

Check if `CHANGELOG.md` needs an update based on the branch changes. Follow [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) format.

---

## 5. Run tests

Run the unit tests for all projects that own changed files:

1. Identify test projects that exercise the changed source files
2. Run those test projects
3. Report any failures — do not mark the review as complete until all tests pass

---

## Output

Report **only actual issues found.** For each issue state: file path, line number, and a one-line description of the violation. If nothing is found, say so.
