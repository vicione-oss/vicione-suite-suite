# Code Style Guide

## C#

### Sorting

Members of a C# `class`, `struct`, `interface` and `record` are sorted by the following groups.
Groups are separated from each other by a line break.

1. Constants
2. Static fields
3. Other fields
4. Properties
5. Events
6. Constructors
   - Grouped by keyword in order of `static`, `public`, `internal`, `protected`, `private` followed by constructors without keyword
   - Groups are sorted by number of parameters in ascending order
7. Methods

### Test methods

- Test method names should use [snake case](https://en.wikipedia.org/wiki/Snake_case) pattern.

  > The Test Explorer replaces underlines with spaces because of [`our configuration`](https://gitlab.i40.ifm-datalink.net/acx/vo-suite/vo-suite/-/blob/6fc3db74e4f7627ead5d562436e588ef44089165/xunit.runner.json#L4) of [`methodDisplayOptions`](https://xunit.net/docs/runsettings#MethodDisplayOptions). As a result, test names like `Should_return_access_level_requirement` are displayed as formulated sentences like `Should return access level requirement`.
