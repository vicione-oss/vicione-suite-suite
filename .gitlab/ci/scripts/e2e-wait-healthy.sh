#!/usr/bin/env bash
# Poll an instance's /hc until it reports healthy (HTTP 200; 503 while still synchronizing).
# For a slave, healthy ⇒ its initial replication from the master is complete. Requires curl.
#
# Usage: e2e-wait-healthy.sh NAME PORT LOGFILE [MAX_ATTEMPTS]
# Polls every 2s, so the timeout is MAX_ATTEMPTS * 2s (default 90 attempts = 180s).

set -o errexit
set -o nounset

name="$1"
port="$2"
log="$3"
max_attempts="${4:-90}"

for attempt in $(seq 1 "${max_attempts}"); do
  if curl -ksf "https://localhost:${port}/hc" >/dev/null 2>&1; then
    echo "${name} healthy on :${port}"
    exit 0
  fi
  if [ "${attempt}" = "${max_attempts}" ]; then
    echo "${name} did not become healthy on :${port} within $((max_attempts * 2))s:"
    tail -n 120 "${log}"
    exit 1
  fi
  sleep 2
done
