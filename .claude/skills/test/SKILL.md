---
name: test
description: Run the full test suite for ViciOne Suite and analyze failures. Use when running tests, validating changes, or diagnosing test failures.
---

# Run tests

Run the full test suite for ViciOne Suite.

```shell
dotnet test vicione-suite.slnx --no-build
```

If tests fail, analyze the failures and suggest fixes. Group failures by category (compilation errors, assertion failures, timeouts).
