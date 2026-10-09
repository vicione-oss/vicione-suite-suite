---
name: pre-mr-review
description: Run pre-MR review checks including translations, code style, consumer idempotency, consumer error handling, and changelog validation. Use before raising a merge request or when reviewing branch changes.
---

# Pre-MR Review

Run this after implementing changes on a branch to catch common issues before raising an MR.

---

## 1. Missing translations

For every `.resx` file changed on the branch:

1. Find all changed resx files: `git diff master...HEAD --name-only | grep '\.resx$'`
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
- Test methods: follow `tests/AGENTS.md` → *Conventions*

**Constructor style**
- Follows `AGENTS.md` → *Code Style & Conventions*

**Test structure**
- Follows `tests/AGENTS.md` → *Arrange / Act / Assert*, including when a marker may be left out

---

## 3. Consumer idempotency

For every new or modified MassTransit consumer on the branch:
- Verify the consumer handles duplicate messages safely
- Instance-dependent state changes (outside `IModuleDbContext`) use instance-dependent consumers, as `AGENTS.md` → *Messaging* requires

---

## 4. Consumer error handling (ADR-004)

For every new or modified consumer on the branch, identify the message kind first — the rules for fire-and-forget
and request consumers are opposites — then check it against `AGENTS.md` → *Messaging*: ADR-004 D1, D6, D6a and
*Compliance*, plus the `[LoggerMessage]` and `*ErrorCodes` rules.

To check the correlation id on a failure log, compare it with the consumer's own success-path logs: if those carry
the correlation id and the error path does not, that is the defect.

---

## 5. Changelog

Check that every change a user, operator or module developer can notice has an entry in `CHANGELOG.md`, and that each
entry follows the *Changelog* rules in `AGENTS.md`: under the unreleased header, one line from the reader's view, no
implementation details. Report entries that explain a cause or a mechanism.

---

## 6. Run tests

Run the unit tests for all projects that own changed files:

1. Identify test projects that exercise the changed source files
2. Run those test projects
3. Report any failures — do not mark the review as complete until all tests pass

---

## Output

Report **only actual issues found.** For each issue state: file path, line number, and a one-line description of the violation. If nothing is found, say so.
