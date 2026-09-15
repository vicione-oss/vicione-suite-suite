# End-to-end UI tests (Playwright)

End-to-end tests drive a real browser ([Playwright](https://playwright.dev/dotnet/)) against a running
**standalone** instance of the Suite. They live in their own project, **`Core.OS.E2E.Tests`**, and are
tagged `Category=E2E`. The project is black-box: it has **no reference to `Core.OS`** and reaches the
running instance over HTTP/the browser only.

The instance under test is resolved from `SUITE_BASE_URL` (default `https://localhost:5001`).
Because the Suite enforces HTTPS behind a certificate the test machine does not trust — a dev certificate locally, nginx's self-signed one in CI — the tests run with `IgnoreHTTPSErrors`.

## Isolation

- **Session** — each test gets its own `IBrowserContext` (own cookies/storage), so logging in during one
  test never leaks into another. The browser process is shared for speed.
- **Execution** — all E2E tests share one xUnit collection, so they run serially against the single
  instance (no concurrent-write races).
- **Backend state** — tests that *intentionally* fail authentication use a throwaway (non-existent) user,
  so lockout-on-failure can never affect the seeded accounts other tests rely on.

## The first-run wizard

A freshly initialized instance redirects **every** authenticated request to its first-run wizard, so the
wizard covers the whole UI until someone leaves it — and only the wizard itself can end that state.
The instances under test therefore run with it switched off:

| Setting                                    | Why                                                                                                                                                                    |
|--------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `FeatureManagement__FirstRunWizard=false`  | The wizard ships **enabled** (`appsettings.json`). With the flag off `OnboardingMiddleware` redirects nothing, so the tests reach the UI of a fresh instance directly.  |

## Credentials

Tests that log in (e.g. `LoginSmokeTests`) authenticate as a user seeded into the instance under test.
The credentials are **not** hard-coded — `TestUsers` resolves them from the environment via
`Core.Tests.Tools.IntegrationServiceSettings.GetRequiredValue`, which throws a clear error when a
variable is missing (there is no default):

| Variable                        | Meaning                                                         |
|---------------------------------|-----------------------------------------------------------------|
| `SUITE_TEST_USERNAME`           | User name of the seeded account to log in with (full access)     |
| `SUITE_TEST_PASSWORD`           | Password of that account                                        |
| `SUITE_TEST_NONADMIN_USERNAME`  | User name of a seeded account **without** full access           |
| `SUITE_TEST_NONADMIN_PASSWORD`  | Password of that account                                        |

They must match accounts the running instance actually seeds (it seeds the built-in test users when
started with `UserManagement__SeedTestUsers=true`).
The non-admin account is what the tests for admin-only panels sign in as, so it must be a seeded user
with `AccessLevel.Partial` — a user seeded with full access is put into the system-administrator role
and would see everything.

- **Locally**, export them in the environment the tests run in — the shell you run `dotnet test`
  from, or the IDE's test run configuration:

  ```bash
  export SUITE_TEST_USERNAME=...
  export SUITE_TEST_PASSWORD=...
  export SUITE_TEST_NONADMIN_USERNAME=...
  export SUITE_TEST_NONADMIN_PASSWORD=...
  ```
- **In CI**, only the admin pair is a project CI/CD variable.
  `SUITE_TEST_USERNAME` and `SUITE_TEST_PASSWORD` are defined **masked but unprotected**
  (Settings → CI/CD → Variables) — a protected variable does not reach merge-request branches,
  which is where the E2E jobs are started by hand.
  They reach every E2E job automatically and are not stored in the YAML.
  The non-admin pair is no secret and is set in the jobs themselves (`e2e-standalone.yml`,
  `e2e-master-slave.yml`): `SUITE_TEST_NONADMIN_USERNAME: Alice`, and
  `SUITE_TEST_NONADMIN_PASSWORD: $SUITE_TEST_PASSWORD`, because all built-in test users are seeded
  with the same password.
  `e2e-check-credentials.sh` fails the job in seconds when one of the four is missing or empty.

## Environment-variable override tests

The `EnvironmentOverrides` tests drive the "Environment variables" panel in the **System** settings
category, which edits the instance's runtime environment-variable override file.
They need one thing from the instance under test: `VICIONE_SUITE_ENV_OVERRIDES=true`, set for every
instance by its role config in `tests/Core.OS.E2E.Tests/.gitlab-ci/conf/`.
It is the whole switch — it makes the instance apply the override file at startup, and it is what
the settings panel reads to decide whether to offer itself, so without it the tests have nothing to
drive.
The file follows each instance's `Instance__HomeDirectory`, so every instance keeps its own.

Two consequences worth knowing before adding a test here:

- **The file outlives the test run**, and it is applied to the process environment the next time that
  instance starts. A leftover variable that means something to the Suite (`Instance__Type`,
  `ConnectionStrings__*`, …) would silently change a later run — a broken value can even keep the
  instance from starting, with no way back through the UI. Every test therefore uses a unique
  `E2E_OVERRIDE_<guid>` name, which nothing reads, and deletes it again.

## Running in CI

Every E2E job lives next to this test project, in `tests/Core.OS.E2E.Tests/.gitlab-ci/`, included from `.gitlab-ci.yml`.
They run **automatically and blocking on the nightly schedule** and stay **manual and non-blocking** everywhere else (tag / default branch / merge request / web).

The instance under test is not a process the job starts.
`Build E2E Image` builds one device-like image — Debian + systemd with the Suite installed from the real `.deb`, nginx terminating TLS — and each E2E job runs that image as GitLab `services:`, every instance picking its role from a per-service `E2E_ROLE` variable and its settings from the matching `conf/<role>.conf`.
The job container is only the test runner (Playwright + dotnet): it builds the test project, polls each instance's `/hc` until healthy (GitLab does not gate on service readiness), and runs the tests.

`E2E Smoke tests (standalone)` needs a single instance (`E2E_ROLE=standalone`), runs on the **medium runner**, and filters `Category=E2E`.
The seeded logins come from CI/CD variables — see [Credentials](#credentials).

Three properties of that image are worth knowing before relying on a run:

- **Which `.deb` goes in.** On the nightly it is the one this pipeline built itself, pinned by package version (`SUITE_DEB_VERSION=$CI_PIPELINE_ID`), so a packaging chain that did not run fails the image build instead of quietly installing a week-old build. Started by hand, it is the newest `.deb` the package registry holds.
- **The image tag `:e2e` is shared.** It is scoped to neither pipeline nor branch, so concurrent image builds overwrite each other and the E2E jobs run whatever the last push left in the registry.
- **The role configs are baked in.** An edit to `conf/*.conf` reaches the instances only after the image has been built again.

Only infrastructure failures are retried (`runner_system_failure`, `stuck_or_timeout_failure`, one attempt).
`script_failure` is deliberately absent: retrying a failing test would turn the nightly's signal into noise.
The same applies to the image build, which is the most network-dependent job of the three.

The timeouts are generous on purpose: 45 minutes for each test job, 20 for the image build, against real runs of about four and one minutes.
They exist to cut off a hung download or a stuck `docker push`, not to bound a slow test.

### The nightly schedule

Nothing in the repository switches the nightly on.
It is a **pipeline schedule on the default branch** (Build → Pipeline schedules) carrying the inputs below.
The job rules read them, and that is the whole switch.

The three switches are declared as [CI/CD inputs](https://docs.gitlab.com/ci/inputs/) in the `spec:` header of `.gitlab-ci.yml` and assigned to variables of the same name, so the rules keep reading plain `$RUN_E2E`.
Inputs rather than schedule variables, because this project's schedule form offers no variables at all: **Minimum role to use pipeline variables** (Settings → CI/CD → Variables) is restricted, and once it is, only a project Owner can lift it.
Inputs are not CI/CD variables and are not covered by that restriction.
Where pipeline variables are allowed, a schedule variable of the same name still overrides the input, so the old setup keeps working.

Every input defaults to `"false"`, which is required: a merge request, branch or tag pipeline passes no inputs, and a missing default fails the pipeline.

| Name             | Set where                                           | What it does                                                                                                                                      | If it is missing                                                                                                                                                                     |
|------------------|-----------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `RUN_E2E`        | nightly schedule input, value `true`                | Runs the image build and both E2E jobs automatically and **blocking** (`allow_failure: false`), and pins the image to this pipeline's `.deb`      | The jobs stay manual and non-blocking, as in any other pipeline                                                                                                                      |
| `BUILD_PACKAGES` | nightly schedule input, value `true`                | Runs the chain that produces the `.deb` — `Build artifacts x64/arm64`, their uploads, `trigger deb packaging` — automatically instead of manually | The chain stays manual, and the image build then fails: it is pinned to this pipeline's package version and finds none. Deliberate — a red job beats an E2E run against an old build |
| `CLEANUP`        | any schedule's input, `true` — none sets it today   | Runs `Cleanup old packages`, a retention job for `ci-artifacts` that predates the nightly and is unrelated to it                                  | The job does not run, which is the state today. It is opt-in on purpose: the nightly leaves it off, and whoever wants retention enables it on a schedule of their own                |
| `E2E_ALERT_URL`  | project CI/CD variable                              | The project's alert endpoint. With `E2E_ALERT_KEY`, lets `Notify Nightly Failure` open a GitLab alert when the nightly goes red                   | Neither notify job exists at all. Adding or removing either variable is the whole switch — no pipeline change, no merge request                                                      |
| `E2E_ALERT_KEY`  | project CI/CD variable                              | That endpoint's authorization key, sent as `Authorization: Bearer`                                                                                | As for `E2E_ALERT_URL`                                                                                                                                                               |

- All three switches are compared against the exact string `"true"` instead of being checked for being set: a bare `$RUN_E2E` is also true for the value `"false"`. The input declares `options: ["true", "false"]`, so the schedule form offers both and `"false"` really does switch the nightly off. A typo is rejected when the pipeline is created, instead of quietly leaving the jobs manual.
- The E2E jobs are **blocking on the nightly on purpose**. The nightly is informational, and a red pipeline is what does the informing: GitLab notifies the schedule's owner when a *pipeline* fails, and never when a job fails with `allow_failure: true`. The price is that a red nightly shows on the default branch's pipeline badge.

### The alert

`Notify Nightly Failure` posts to the project's own alert endpoint, and the alert *is* the notification: who gets emailed or paged is decided by the project's alert settings, not by anything in this repository.

Both jobs send the same `fingerprint`, `nightly-e2e`.
GitLab groups repeated failures under that fingerprint into one alert, so a nightly that stays red pages once instead of every night.
`Resolve Nightly Alert` then sends that fingerprint with an `end_time` on the next green run, which closes the alert and re-arms the notification for the next real failure — without it, a grouped alert would stay open and the next failure would page nobody.

A resolve for a fingerprint with no open alert is answered with `400`, not with a 2xx.
GitLab never creates an alert from a resolving payload, so the request ends on `return bad_request unless alert.persisted?`.
For a nightly that was already green that is the normal case, so `Resolve Nightly Alert` sets `ALERT_TOLERATED_STATUS: "400"` and reports it as nothing to do.
`Notify Nightly Failure` leaves that variable empty and still fails on any non-2xx, which is what surfaces a wrong `E2E_ALERT_URL` or `E2E_ALERT_KEY`.

GitLab's own *Notify only when pipeline status changes* option, on the Pipeline status emails and chat integrations, would give the same break/fix notification without any of this.
It does not fit here because it tracks `broken` and `fixed` per branch — `Ci::Ref` is keyed by project and ref path alone.
Every ordinary `master` pipeline would move that state, so a merge on the morning after a red nightly would report the nightly as recovered without re-running it.

The alert body links to the pipeline's failed jobs, its test report and its Jobs tab.
The Playwright traces are in the E2E job's artifact archive under `bin/Debug/net10.0/traces/`, so they are two clicks from the alert: the Jobs tab, then the artifacts download icon on the failed job.
Those links die with the artifacts after a week (`expire_in: 1 week` in `e2e-base.yml`).

The YAML files carry no more than a pointer back here; the reasoning lives in this document.

## Running locally

Create the dev certificate once (`dotnet dev-certs https --trust`), start a standalone instance
(e.g. `dotnet run --project src/Core.OS --launch-profile Standalone-Ui`, adding
`UserManagement__SeedTestUsers=true` so the accounts the tests sign in with exist and
`FeatureManagement__FirstRunWizard=false` so a fresh instance is not redirected to the wizard),
install browsers (`pwsh tests/Core.OS.E2E.Tests/bin/Debug/net10.0/playwright.ps1 install
chromium-headless-shell`),
provide the seeded-user credentials (see [Credentials](#credentials)), then:

```bash
dotnet test tests/Core.OS.E2E.Tests/Core.OS.E2E.Tests.csproj --filter-query "/[Category=E2E]"
```

A launch-profile instance serves its static assets debug-style, unlike the installed `.deb` behind nginx that CI tests.
That is fine for iterating on tests, but it is not a reproduction of the CI setup, which has no local equivalent.


# End-to-end master/slave tests (Playwright)

A second E2E variant runs a small **master + 2 slaves** topology and exercises concerns that only exist
there — replication and master/slave roles — which the standalone variant cannot cover. The
tests live in the same `Core.OS.E2E.Tests` project but are tagged **`Category=E2E-MasterSlave`**, so the
two suites are disjoint (the standalone job filters `Category=E2E`, the master/slave job `Category=E2E-MasterSlave`).

## Master/slave architecture

| Instance | Database                       | Role                                                                           |
|----------|--------------------------------|--------------------------------------------------------------------------------|
| Master   | **PostgreSQL**                 | Single source of truth for global state                                        |
| Slave    | **in-memory SQLite** (replica) | Replicates the master's state over RabbitMQ; cannot write global state locally |

What this means for the harness:

- **Only the master uses Postgres.** Slaves don't connect to any DB server — they hold an in-memory
  SQLite replica. The topology needs exactly **one Postgres** (for the master), not one per slave.
- **RabbitMQ is required.** All instances run with `MessageBus__UseInMemoryBus=false` against the same
  broker (the in-memory bus is per-process and can't cross instances).
- **Only the master seeds users** (`SeedTestUsers=true`); slaves run with `SeedTestUsers=false` and
  obtain users only via replication.
- The replication path is **eventually consistent**, so tests against replicated state poll-until
  (an "eventually" assertion with a timeout) rather than asserting immediately.

## Readiness: `/hc`

Every instance exposes `GET /hc` with a **"Synchronization"** health check that stays unhealthy until
the instance's initial sync completes, then turns healthy. The instances are brought up by **polling `/hc`
per instance until healthy** — for a slave, healthy means its initial replication from the master is
done. This is more reliable than waiting for the port to bind (a slave binds its port long before it
has synced).

## Structure

```
tests/Core.OS.E2E.Tests/
  Infrastructure/
    MasterSlaveE2ETest.cs     # base class: opens an isolated page against a chosen instance
  MasterSlave/
    UserReplicationSmokeTests.cs
    EnvironmentOverridesIsolationSmokeTests.cs
```

- `MasterSlaveE2ETest` resolves the instance URLs from the environment — `SUITE_MASTER_URL`,
  `SUITE_SLAVE1_URL`, `SUITE_SLAVE2_URL` (defaults `https://localhost:5001` / `:6001` / `:7001`, matching
  the launch profiles) — and exposes `NewPage(url)` so a test drives a specific instance. The browser and
  the page objects (`LoginPage`) are shared with the standalone tests, as is switching the
  [first-run wizard](#the-first-run-wizard) off for every instance.
- The first test, **`UserReplicationSmokeTests`**, logs in on **each slave** with the master-seeded user
  (with a bounded retry to absorb replication lag). Because slaves never seed, a successful slave login
  proves the account replicated.

## Running in CI

`E2E Smoke tests (master/slave)` uses the same image, trigger and job shape as the standalone job (see [Running in CI](#running-in-ci) above) and differs only in its topology: three instances of that image as services — `E2E_ROLE=master`, `slave1`, `slave2` — next to **Postgres** (for the master) and **RabbitMQ** (for the replication), on the **large runner**, filtering `Category=E2E-MasterSlave`.
A slave registers with the master exactly once at startup, so each slave container holds its own boot until the master's `/hc` answers, before the job's own readiness polling begins.

## Running locally

There is no local equivalent of CI's `.deb`/systemd/nginx topology — locally the instances run from their launch profiles.
Bring up the backing services (Postgres, RabbitMQ, Aspire Dashboard):

```bash
docker compose -f tests/compose.master-slave.yaml up -d
```

Then run the three instances from their launch profiles, each in its own terminal — `Master-Ui`,
`Slave1-Ui`, `Slave2-Ui` (`dotnet run --project src/Core.OS --launch-profile <profile>`), each with
`FeatureManagement__FirstRunWizard=false`. With the instances on the default ports
(`:5001` / `:6001` / `:7001`) and the seeded-user credentials available
(see [Credentials](#credentials)):

```bash
dotnet test tests/Core.OS.E2E.Tests/Core.OS.E2E.Tests.csproj --filter-query "/[Category=E2E-MasterSlave]"
```
