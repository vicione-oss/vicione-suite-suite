#!/usr/bin/env bash
# Fail fast when the E2E login credentials are missing or empty, so the job aborts in seconds
# instead of failing after the full publish with an opaque login error.

set -o errexit
set -o nounset

# The non-admin account is needed by the tests that assert an admin-only panel stays hidden; it
# must be a seeded user without full access (the seeded Alice/Bob have AccessLevel.Partial).
for var in SUITE_TEST_USERNAME SUITE_TEST_PASSWORD SUITE_TEST_NONADMIN_USERNAME SUITE_TEST_NONADMIN_PASSWORD; do
  if [ -z "${!var:-}" ]; then
    echo "FATAL: ${var} is not set or empty."
    echo "Export the seeded login credentials first (in CI: a masked project CI/CD variable, Settings -> CI/CD -> Variables). See docs/e2e-testing.md."
    exit 1
  fi
done
echo "Admin and non-admin seeded-account credentials are set."
