#!/usr/bin/env bash
# Fail fast when the E2E login credentials are missing or empty, so the job aborts in seconds
# instead of failing after the full publish with an opaque login error.

set -o errexit
set -o nounset

for var in SUITE_TEST_USERNAME SUITE_TEST_PASSWORD; do
  if [ -z "${!var:-}" ]; then
    echo "FATAL: ${var} is not set or empty."
    echo "Export the seeded login credentials first (in CI: a masked project CI/CD variable, Settings -> CI/CD -> Variables). See docs/e2e-testing.md."
    exit 1
  fi
done
echo "SUITE_TEST_USERNAME and SUITE_TEST_PASSWORD are set."
