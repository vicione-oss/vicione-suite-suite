#!/usr/bin/env bash
# Prepare an E2E job: publish the Suite under test and build the test project.
# Expects the toolchain (dotnet, node) to be on PATH already (mise activated by the job).

set -o errexit
set -o nounset

# HTTPS dev certificate (the Suite enforces HTTPS redirection).
dotnet dev-certs https

# Publish the instance(s) under test into a production-like layout, with the same script
# production packaging uses (PLATFORM defaults to linux-x64, matching the runner); the E2E
# test project itself stays a plain build (it is run via dotnet test, not published).
PUBLISH_DIRECTORY=publish/e2e bash build/publish-suite.sh

# Copy only the linux-x64 Playwright driver (CI is linux/amd64); the default copies every x64
# platform driver (~200MB more in the test bin).
dotnet build tests/Core.OS.E2E.Tests/Core.OS.E2E.Tests.csproj -p:PlaywrightPlatform=linux-x64
