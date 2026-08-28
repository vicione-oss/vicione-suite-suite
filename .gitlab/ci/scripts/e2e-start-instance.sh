#!/usr/bin/env bash
# Start one Suite instance from the published layout, from inside the publish dir — like
# production does (build/package_settings.sh) — so the published appsettings.json is picked up
# from the content root and relative state and module paths resolve into the publish output.
# The log path is resolved relative to the caller's working directory (CI_PROJECT_DIR in CI).
#
# Usage: [role-specific env] e2e-start-instance.sh NAME HTTP_PORT HTTPS_PORT LOGFILE
# The caller backgrounds the script with `&` and passes role-specific configuration
# (Instance__Type, UserManagement__SeedTestUsers, connection strings, ...) via the environment.

set -o errexit
set -o nounset

name="$1"
http_port="$2"
https_port="$3"
log="$4"

caller_dir="$(pwd)"
cd publish/e2e
export Kestrel__Endpoints__Http__Url="http://localhost:${http_port}"
export Kestrel__Endpoints__Https__Url="https://localhost:${https_port}"
export Instance__HomeDirectory="AppData_${name}"
export Instance__CacheDirectory="Cache_${name}"
export Instance__BackupDirectory="Backup_${name}"
# Runtime environment-variable overrides are off unless switched on, and the EnvironmentOverrides
# tests need them on. Each instance keeps its own file without further setup: the location follows
# Instance__HomeDirectory, which is per-instance above.
export VICIONE_SUITE_ENV_OVERRIDES=true
exec ./ViciOne.Suite.Core.OS > "${caller_dir}/${log}" 2>&1
