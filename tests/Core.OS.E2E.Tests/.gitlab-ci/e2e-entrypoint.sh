#!/bin/sh
# The systemd-managed suite reads its configuration from EnvironmentFiles, not from the container
# environment, so we cannot configure a role via a plain env var. Instead, the per-instance role is
# passed as the E2E_ROLE container variable and this entrypoint activates the matching config file
# (baked into /opt/e2e-conf/) before handing PID 1 over to systemd.
set -e

if [ -n "${E2E_ROLE:-}" ]; then
  role_conf="/opt/e2e-conf/${E2E_ROLE}.conf"
  if [ ! -f "$role_conf" ]; then
    echo "e2e-entrypoint: E2E_ROLE='${E2E_ROLE}' but ${role_conf} does not exist" >&2
    exit 1
  fi
  install -m 0644 "$role_conf" /etc/vicione-suite/conf.d/99-e2e.conf
fi

# A slave sends its RegisterInstance once at startup and never re-sends it (ApplicationWorker);
# SyncRetryState only counts routing-slip faults, so a registration nobody answers leaves the
# instance unsynchronized and its /hc at 503 for the life of the container. GitLab starts all
# services in parallel with no ordering, so gate the boot here: wait for E2E_WAIT_FOR_URL (the
# master's health endpoint, whose readiness also implies the bus is up) before starting systemd.
# Self-signed cert on a hostname that does not match its CN, hence curl -k.
if [ -n "${E2E_WAIT_FOR_URL:-}" ]; then
  max_attempts="${E2E_WAIT_FOR_MAX_ATTEMPTS:-150}"
  attempt=0
  until curl -ksf "$E2E_WAIT_FOR_URL" >/dev/null 2>&1; do
    attempt=$((attempt + 1))
    if [ "$attempt" -ge "$max_attempts" ]; then
      echo "e2e-entrypoint: ${E2E_WAIT_FOR_URL} did not answer within $((max_attempts * 2))s; not starting" >&2
      exit 1
    fi
    sleep 2
  done
  echo "e2e-entrypoint: ${E2E_WAIT_FOR_URL} is up after $((attempt * 2))s; starting"
fi

exec /sbin/init
