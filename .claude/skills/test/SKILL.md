---
name: test
description: Run the full test suite for ViciOne Suite and analyze failures. Use when running tests, validating changes, or diagnosing test failures.
---

# Run tests

```shell
dotnet test vicione-suite.slnx --no-build --filter "Category!=ManualDbTest&Category!=System&Category!=Integration"
```

If tests fail, analyze the failures and suggest fixes. Group failures by category (compilation errors, assertion failures, timeouts).
