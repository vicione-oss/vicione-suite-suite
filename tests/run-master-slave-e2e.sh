#!/usr/bin/env bash
#
# Spin up the master/slave backing services (Postgres, RabbitMQ, Aspire Dashboard) and a local
# master + 2 slaves, then run the master/slave end-to-end tests (Category=E2E-MasterSlave).
#
# The instances run from a published layout (build/publish-suite.sh) — the same way the CI job and
# production run the Suite — so static web assets are served production-style and the run needs
# no appsettings.Development.json. Instance state (AppData_* etc.) lives inside the publish dir.
# Instance startup, health polling, and the credential check are shared with the CI job
# (.gitlab/ci/e2e-master-slave-tests.yml) via the scripts in .gitlab/ci/scripts/.
# See docs/e2e-testing.md.
#
# Usage:
#   tests/run-master-slave-e2e.sh          # ephemeral run: resets state first, stops the services afterwards
#   tests/run-master-slave-e2e.sh --keep   # reuse existing state and leave the services running afterwards
#                                          # (binaries are re-published either way)
#
# The login test needs the credentials of the account the master seeds (see docs/e2e-testing.md):
#   SUITE_TEST_USERNAME=... SUITE_TEST_PASSWORD=... tests/run-master-slave-e2e.sh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${REPO_ROOT}"

COMPOSE_FILE="tests/compose.master-slave.yaml"
PUB="${REPO_ROOT}/publish/e2e"
E2E_PROJECT="tests/Core.OS.E2E.Tests/Core.OS.E2E.Tests.csproj"
E2E_BIN="tests/Core.OS.E2E.Tests/bin/Debug/net10.0"

# Seeded account the login test signs in with; must match what the master seeds.
# No defaults on purpose: credentials must not live in source (see docs/e2e-testing.md).
bash .gitlab/ci/scripts/e2e-check-credentials.sh

KEEP=0
[[ "${1:-}" == "--keep" ]] && KEEP=1

PIDS=()
# shellcheck disable=SC2317  # invoked via the EXIT trap, not inline
cleanup() {
  echo "==> Stopping suite instances ..."
  if [[ ${#PIDS[@]} -gt 0 ]]; then
    kill "${PIDS[@]}" 2>/dev/null || true
    wait "${PIDS[@]}" 2>/dev/null || true
  fi
  if [[ "${KEEP}" -eq 1 ]]; then
    echo "==> Leaving backing services running (--keep). Stop them with: docker compose -f ${COMPOSE_FILE} down"
  else
    echo "==> Stopping backing services ..."
    docker compose -f "${COMPOSE_FILE}" down >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

# Settings shared by all instances (module dir, mock host management, console logging for the
# *.log files, local RabbitMQ, OTLP export as in the launch profiles) — mirrors the `variables:`
# block of the CI master/slave job, with local backing-service hosts/credentials instead.
export ASPNETCORE_ENVIRONMENT=Development
export ModuleLoader__ModulesPath=Modules
export HostManagement__MockClient__DataSource=SystemConfigurationEmbedded
export Logging__LogTargets__0=Console
export MessageBus__UseInMemoryBus=false
export MessageBus__CleanVirtualHost=false
export MessageBus__Connection__Host=localhost
export MessageBus__Connection__Port=5672
export MessageBus__Connection__User=guest
export MessageBus__Connection__Pass=guest
export OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:4317"

if [[ "${KEEP}" -eq 0 ]]; then
  echo "==> Clean run: resetting the master DB and instance state (use --keep to reuse) ..."
  docker compose -f "${COMPOSE_FILE}" down -v >/dev/null 2>&1 || true
  rm -rf "${PUB}"
fi

echo "==> Starting backing services (Postgres, RabbitMQ, Aspire Dashboard) ..."
docker compose -f "${COMPOSE_FILE}" up -d --wait

echo "==> Ensuring HTTPS dev certificate ..."
dotnet dev-certs https >/dev/null

# Publish the instances under test into a production-like layout with the production packaging
# script (the same set as the CI master/slave job), then build the E2E test project (run via
# dotnet test, not published). PLATFORM is the host RID — unlike CI, a local machine may not be
# linux-x64, and a self-contained publish for the wrong RID would not start.
PUBLISH_DIRECTORY="${PUB}" PLATFORM="$(dotnet --info | awk '/RID:/{print $2; exit}')" bash build/publish-suite.sh
for path in "${PUB}/ViciOne.Suite.Core.OS" "${PUB}/UiHosts/Blazor.Server/wwwroot"; do
  if [[ ! -e "${path}" ]]; then
    echo "!!! Expected publish output missing: ${path}"
    exit 1
  fi
done
dotnet build "${E2E_PROJECT}" --nologo -v minimal

echo "==> Ensuring Playwright Chromium headless shell ..."
# Run Playwright's install CLI with node from PATH (provided by mise — see mise.toml). This avoids
# having to pick the right per-platform binary out of the bundled .playwright/node directory.
if ! command -v node >/dev/null 2>&1; then
  echo "!!! 'node' not found on PATH. It is managed by mise (mise.toml: node 24) — run 'mise install' / activate mise."
  exit 1
fi
node "${E2E_BIN}/.playwright/package/cli.js" install chromium-headless-shell

# Master first, and wait until it is healthy so seeding is done before any slave syncs.
echo "==> Starting master ..."
Instance__Type=Master \
ConnectionStrings__Postgres="Server=127.0.0.1;Port=5432;Database=vo-suite;User Id=postgres;Password=postgres" \
UserManagement__SeedTestUsers=true \
OTEL_SERVICE_NAME=vicione-master \
bash .gitlab/ci/scripts/e2e-start-instance.sh Master 5000 5001 master.log &
PIDS+=("$!")
bash .gitlab/ci/scripts/e2e-wait-healthy.sh master 5001 master.log

# Then the slaves; wait for each to finish its initial replication from the master.
echo "==> Starting slave1 ..."
Instance__Type=Slave \
UserManagement__SeedTestUsers=false \
OTEL_SERVICE_NAME=vicione-slave1 \
bash .gitlab/ci/scripts/e2e-start-instance.sh Slave1 6000 6001 slave1.log &
PIDS+=("$!")
echo "==> Starting slave2 ..."
Instance__Type=Slave \
UserManagement__SeedTestUsers=false \
OTEL_SERVICE_NAME=vicione-slave2 \
bash .gitlab/ci/scripts/e2e-start-instance.sh Slave2 7000 7001 slave2.log &
PIDS+=("$!")
bash .gitlab/ci/scripts/e2e-wait-healthy.sh slave1 6001 slave1.log
bash .gitlab/ci/scripts/e2e-wait-healthy.sh slave2 7001 slave2.log

echo "==> Running master/slave E2E tests ..."
set +e
dotnet test "${E2E_PROJECT}" --no-build --filter-query "/[Category=E2E-MasterSlave]"
TEST_RC=$?
set -e

if [[ "${TEST_RC}" -ne 0 ]]; then
  echo "==> Tests failed (exit ${TEST_RC})."
  echo "    Failure traces: ${E2E_BIN}/traces  (open with: playwright show-trace <file>)"
  echo "    Instance logs:  master.log, slave1.log, slave2.log"
fi

exit "${TEST_RC}"
