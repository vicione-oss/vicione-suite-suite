# End-to-end UI tests (Playwright)

End-to-end tests drive a real browser ([Playwright](https://playwright.dev/dotnet/)) against a running
**standalone** instance of the Suite. They live in their own project, **`Core.OS.E2E.Tests`**, and are
tagged `Category=E2E`. The project is black-box: it has **no reference to `Core.OS`** and reaches the
running instance over HTTP/the browser only.

The instance under test is resolved from `SUITE_BASE_URL` (default `https://localhost:5001`). Because the
Suite enforces HTTPS redirection with a dev certificate, the tests run with `IgnoreHTTPSErrors`.

## Structure

```
tests/Core.OS.E2E.Tests/
  Infrastructure/                 # the harness
    PlaywrightFixture.cs          #   shared browser, launched once per run
    E2ECollectionDefinition.cs    #   xUnit collection: shared browser + serial execution
    E2ETest.cs                    #   base class: a fresh, isolated Page per test
    TestUsers.cs                  #   seeded-user credentials
  Pages/                          # page objects (selectors + actions), e.g. LoginPage
    SettingsPage.cs               #   the settings popup and its navigation tree
    EnvironmentOverridesPanel.cs  #   the "Environment variables" control panel
    EnvironmentOverridesUi.cs     #   one instance's panel, opened and ready to work with
    MessageBanner.cs              #   the layout's message banner
  Availability/
    AvailabilitySmokeTests.cs     # "instance serves the UI"
  Authentication/                 # tests grouped by feature
    LoginSmokeTests.cs
  EnvironmentOverrides/
    EnvironmentOverridesSmokeTests.cs               # round trip, restart requirement, refused save
    EnvironmentOverridesAuthorizationSmokeTests.cs  # admin-only panel stays hidden
```

- **Page objects** (`Pages/`) own the selectors and actions for a screen, so tests read as intent and a
  markup change is fixed in one place.
- Tests are grouped into per-feature folders/namespaces (e.g. `Authentication`) so they are easy to find.

## Adding a smoke test

Derive from `E2ETest`, opt into the collection, and tag the category:

```csharp
using Core.OS.E2E.Tests.Infrastructure;
using Core.Tests.Tools;

[Collection(E2ECollectionDefinition.Name)]
[Trait(Traits.Category, Traits.E2E)]
public sealed class MyFeatureSmokeTests(PlaywrightFixture fixture) : E2ETest(fixture)
{
    [Fact]
    public async Task Does_the_thing()
    {
        await Page.GotoAsync("/...");   // Page is a fresh, isolated browser page
        // assert via Playwright's Expect(...), ideally through a page object in Pages/
    }
}
```

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

- **Locally**, export them in the environment the tests run in — the shell you run `dotnet test` (or
  the wrapper scripts) from, or the IDE's test run configuration:

  ```bash
  export SUITE_TEST_USERNAME=...
  export SUITE_TEST_PASSWORD=...
  export SUITE_TEST_NONADMIN_USERNAME=...
  export SUITE_TEST_NONADMIN_PASSWORD=...
  ```
- **In CI**, they are defined as **masked** (and protected) project or group CI/CD variables
  (Settings → CI/CD → Variables). They are injected into the `E2E tests (standalone)` job
  automatically and are not stored in the YAML.

## Environment-variable override tests

The `EnvironmentOverrides` tests drive the "Environment variables" panel in the **System** settings
category, which edits the instance's runtime environment-variable override file.
They need one thing from whoever starts the instance: `VICIONE_SUITE_ENV_OVERRIDES=true`, exported
for every instance by `e2e-start-instance.sh`.
It is the whole switch — it makes the instance apply the override file at startup, and it is what
the settings panel reads to decide whether to offer itself, so without it the tests have nothing to
drive.
The file follows each instance's `Instance__HomeDirectory`, so every instance of a master/slave
topology keeps its own inside a single job container.

Two consequences worth knowing before adding a test here:

- **The file outlives the test run**, and it is applied to the process environment the next time that
  instance starts. A leftover variable that means something to the Suite (`Instance__Type`,
  `ConnectionStrings__*`, …) would silently change a later run — a broken value can even keep the
  instance from starting, with no way back through the UI. Every test therefore uses a unique
  `E2E_OVERRIDE_<guid>` name, which nothing reads, and deletes it again.

## The published layout

The CI jobs (and the master/slave wrapper script) do **not** start the instances with `dotnet run`.
A built (non-published) app serves its static web assets debug-style, through the
`staticwebassets` manifest pointing back into the source tree, so HTTP resource requests can differ
from what a production deployment serves. Instead, the production packaging script publishes the
Suite into the production layout — the host, the UI host under `UiHosts/Blazor.Server`, and a
`Modules/` directory:

```bash
PUBLISH_DIRECTORY=publish/e2e bash build/publish-suite.sh
```

The instances are started from inside that directory, exactly like production starts the Suite.
The UI host then serves its **published `wwwroot`**.

The script publishes Core.OS **self-contained** — that is required, not an implementation detail.
The module load context defers shared-framework assemblies (e.g.
`Microsoft.AspNetCore.Components.Forms`) to the default context by matching them against Core.OS's
runtime files, which only list those assemblies in a self-contained publish. A framework-dependent
Release Core.OS leaves them unmatched, so the dynamically-loaded UI host loads its own copies into a
separate load context and breaks (`EditForm`/`EditContext` `InvalidCastException`).

`PLATFORM` defaults to `linux-x64` (matching CI and production); on a local machine with a different
RID, pass the host RID, as `tests/run-master-slave-e2e.sh` does — a self-contained publish for the
wrong RID would not start. A re-publish overwrites the output in place but does not wipe it, so
instance state directories (`AppData_*` etc.) living inside it survive.

Two consequences to be aware of:

- `appsettings.Development.json` is excluded from a publish, so everything the instances need is
  passed as **explicit environment variables** in the CI YAML / wrapper script (module directory,
  in-memory bus for standalone, mock host management, console logging, Kestrel endpoints, ...).
- `ASPNETCORE_ENVIRONMENT` stays `Development` **only** because the sample modules (Burger, JiTChat)
  are activated in Development; outside it, modules come from the artifact-repository/manifest
  mechanism, where the samples don't exist. Static assets are served production-style regardless —
  that is controlled by `UseDebugRoot`, which is only enabled by the (unpublished) Development
  settings file. Don't "fix" the environment to Production without solving module activation.

## Running in CI

The `E2E Smoke tests (standalone)` job (defined in `.gitlab/ci/e2e-standalone-tests.yml` and included from `.gitlab-ci.yml`)
is **manual** — triggered with the play button — and non-blocking (`allow_failure` for now):

1. creates an HTTPS dev certificate (`dotnet dev-certs https`) and publishes the Suite into the
   production-like layout (`build/publish-suite.sh`, see [The published layout](#the-published-layout));
2. installs Chromium via Playwright's bundled node + CLI (no PowerShell required; browsers cached);
3. starts a standalone instance in the background from the publish directory with seeded test users
   (`UserManagement__SeedTestUsers=true`) and polls `/hc` until it is healthy;
4. runs `dotnet test tests/Core.OS.E2E.Tests/Core.OS.E2E.Tests.csproj --filter-query "/[Category=E2E]"`.

The login test also needs `SUITE_TEST_USERNAME` / `SUITE_TEST_PASSWORD`, supplied as masked project
CI/CD variables (they are not in the YAML) — see [Credentials](#credentials).

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

A launch-profile instance serves debug-style static assets — fine for iterating on tests. To
reproduce the CI setup exactly, publish and start from the publish directory instead (see
[The published layout](#the-published-layout), then run `./publish/e2e/ViciOne.Suite.Core.OS` from
that directory with the environment variables the standalone job sets in
`.gitlab/ci/e2e-standalone-tests.yml`).


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
  [first-run wizard](#the-first-run-wizard) off — the wrapper script and the CI job set it for every
  instance they start.
- The first test, **`UserReplicationSmokeTests`**, logs in on **each slave** with the master-seeded user
  (with a bounded retry to absorb replication lag). Because slaves never seed, a successful slave login
  proves the account replicated.

## Running in CI

The `E2E Smoke tests (master-slave)` job (defined in `.gitlab/ci/e2e-master-slave-tests.yml`, included from
`.gitlab-ci.yml`) is **manual** and non-blocking (`allow_failure`):

1. provides **Postgres** and **RabbitMQ** as GitLab `services:`;
2. publishes the Suite into the production-like layout (`build/publish-suite.sh`), builds the test
   project, and installs the Chromium headless shell;
3. starts the **master** (`:5001`, Postgres, `SeedTestUsers=true`) from the publish directory and
   waits for `/hc` healthy — so seeding is finished before any slave syncs;
4. starts **slave1 (`:6001`)** and **slave2 (`:7001`)** (`SeedTestUsers=false`) and waits for each `/hc`;
5. runs `dotnet test … --filter-query "/[Category=E2E-MasterSlave]"`.

It runs on the **large runner** (master + 2 slaves + Postgres + RabbitMQ + a browser), and uses the same
`SUITE_TEST_USERNAME` / `SUITE_TEST_PASSWORD` credentials as the standalone job (see [Credentials](#credentials)).

## Running locally

The wrapper script `tests/run-master-slave-e2e.sh` does everything below in
one go: backing services, publish, instance startup gated on `/hc`, test run, teardown.
It needs the seeded-user credentials exported in the shell environment (see
[Credentials](#credentials)).
By default the run is ephemeral (state is reset first, services are stopped afterwards); pass
`--keep` to reuse existing state and leave the services running (instance state lives inside
`publish/e2e`, which a re-publish overwrites but does not wipe).

```bash
SUITE_TEST_USERNAME=... SUITE_TEST_PASSWORD=... tests/run-master-slave-e2e.sh
```

For quick interactive work (not the published CI setup), bring up the backing services (Postgres,
RabbitMQ, Aspire Dashboard):

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
