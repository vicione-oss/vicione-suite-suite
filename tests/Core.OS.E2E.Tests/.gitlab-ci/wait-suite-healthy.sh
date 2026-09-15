#!/usr/bin/env bash
# Poll a suite service container's /hc until it reports healthy (HTTP 200; 503 while still
# synchronizing). For a slave, healthy ⇒ its initial replication from the master is complete.
# The instances serve HTTPS with a self-signed certificate whose CN does not match the service
# alias, hence curl -k. Requires curl.
#
# Usage: wait-suite-healthy.sh NAME BASE_URL [MAX_ATTEMPTS]
# Polls every 2s, so the timeout is MAX_ATTEMPTS * 2s (default 90 attempts = 180s).
#
# On failure there is no log to go on: a service container's output reaches the job trace only with
# CI_DEBUG_SERVICES, which is off because it would also trace the masked credentials the service is
# handed. Add it to the job's variables for a debugging run — that is also where a denied
# --privileged shows up.

set -o errexit
set -o nounset

name="$1"
base_url="$2"
max_attempts="${3:-90}"

for attempt in $(seq 1 "${max_attempts}"); do
  if curl -ksf "${base_url}/hc" >/dev/null 2>&1; then
    echo "${name} healthy at ${base_url}"
    exit 0
  fi
  if [ "${attempt}" = "${max_attempts}" ]; then
    echo "${name} did not become healthy at ${base_url} within $((max_attempts * 2))s;"
    echo "to see the instance's own log, add CI_DEBUG_SERVICES to the job's variables and re-run."
    exit 1
  fi
  sleep 2
done
