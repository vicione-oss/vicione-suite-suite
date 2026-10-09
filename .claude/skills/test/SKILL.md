---
name: test
description: Run the full test suite for ViciOne Suite and analyze failures. Use when running tests, validating changes, or diagnosing test failures.
---

# Run tests

Run the test command from `AGENTS.md` → *Build & Run*. It excludes the infrastructure-heavy categories,
matching CI's unit-test job.

This repo runs on **Microsoft.Testing.Platform (MTP)**, so filters use `--filter-query` — the VSTest-style
`--filter "Category!=..."` matches zero tests here.

Note: an exit code of 1 together with `failed: 0` is expected — the `Core.OS.E2E.Tests` project reports
"Zero tests ran" once the E2E categories are excluded (CI runs E2E in separate jobs). It is not a failure.

The excluded categories need external infrastructure (full app host / bound ports, PostgreSQL, SMTP,
Playwright) and are run in dedicated CI jobs.

If tests fail, analyze the failures and suggest fixes. Group failures by category (compilation errors, assertion failures, timeouts).
