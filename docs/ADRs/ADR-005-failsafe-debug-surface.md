# ADR-005: Failsafe Debug Surface

## Status

Proposed

## Date

2026-08-31

## Context

When the Suite cannot complete startup it runs the failsafe host built by `Core.OS/Hosting/Services/FallbackHostBuilder.cs`, under status `invalid_options` or `recovery_exhausted`.
That host answers `/` with a JSON payload of `status`, `messages` and `timestamp`, and `/health` with an unhealthy check carrying the joined failure description.
For an operator on an Edge device this is the only surface left, and it carries nothing beyond the raw failure messages and offers no way to act.

The motivating failure is the runtime env-var override file (`env-overrides.env`).
A file that parses cleanly but carries a fatal value — an OIDC provider configured with 3 of 4 required values, for instance — drives the Suite into `invalid_options`, and the file deliberately outlives every restart, reset and restore.
A *malformed* file already degrades to the inherited environment in `EnvironmentOverridesLoader`, so the fatal-value case is the unrecoverable one.
The Suite reports the offending option but cannot attribute it to the override file without major effort, so today the only way back is deleting the file through shell access.

### Constraints

- The failsafe host runs *because* configuration is broken: `InstanceOptions.HomeDirectory` may itself be the invalid value.
- Edge deployments run under `Restart=always`, so a failsafe host that throws is a restart loop, not a visible error.
- The surface is reachable without authentication, exactly like the existing version-downgrade page.
- Blazor lives in the `Blazor.Server.Backend` module, which the failsafe host does not load; interactive render mode is out of reach.

## Options Considered

### D1 — HTML page or extended JSON

- **A: HTML page, drop the JSON.** Carries a CTA. Loses a machine-readable `/`.
- **B: HTML plus JSON via content negotiation.** Keeps both. Two response paths to build and test, for a consumer that does not exist.
- **C: Extended JSON only.** No new rendering. Cannot carry an operator action at all.

### D2 — Which diagnostic groups

- **A: Suite, SDK and data version · failsafe status and messages · recovery state · env-var overrides.** All four are readable from disk without a working DI container.
- **B: The above plus last startup errors.** Nothing persists startup errors structurally — only the Serilog file — so this means putting a log tail on an unauthenticated page.

### D3 — Show or mask override values

- **A: Mask values, show key names.**
- **B: Show full values.** Maximum diagnostic value, but override values are where secrets land.

### D4 — Delete the override file or rename it aside

- **A: Rename aside to one fixed name.** Recoverable and self-limiting.
- **B: Delete.** Simplest; the evidence of what broke the boot is gone.
- **C: Rename aside with a timestamp suffix.** Keeps every generation; files accumulate in the home directory over repeated failures.

### D5 — Healthy-boot marker that cleans up automatically

- **A: Rule it out.** The operator action stays the only trigger.
- **B: Implement it.** Removes the need for an operator, at the price of a false positive.

### D6 — GET link or POST form for the action

- **A: POST form.**
- **B: GET link.** Matches the existing downgrade page; a browser prefetch or link scanner can fire it.

## Decision

| #  | Decision                                                                                                     | Reason                                                                                                                                                                                                                                                                                                          |
|----|--------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| D1 | **A** — render HTML on `/` and drop the JSON                                                                 | The AC requires an action an operator triggers without shell access, and JSON cannot carry a CTA. No consumer of the `/` JSON was found in `suite`, `hostmgmt`, `deb-packaging`, `deployments` or `build` — only the Suite's own unit tests reference it. `/health` stays the machine surface and is unchanged. |
| D2 | **A** — four groups: Suite, SDK and data version, failsafe status and messages, recovery state, env-var overrides | All four are reachable without DI and without a valid configuration. A log tail leaks far more than the four groups together.                                                                                                                                                                                   |
| D3 | **A** — mask values, show key names                                                                          | The surface has no authentication. `EnvironmentOverridesRepository` already logs keys only, for the same reason; the page must not be weaker than the log.                                                                                                                                                      |
| D4 | **A** — rename to the fixed name `env-overrides.env.disabled`                                                | The operator can still read what broke the boot. A fixed name cannot pile up files. `EnvironmentOverridesFile.ResolvePath` resolves only `env-overrides.env`, so the renamed file is inert on the next boot with no loader change.                                                                              |
| D5 | **A** — ruled out                                                                                            | A boot can fail with perfectly valid overrides (an unreachable DB server), so the marker would delete a good file. The operator action is the safe baseline.                                                                                                                                                    |
| D6 | **A** — POST form                                                                                            | The downgrade page triggers its data-wiping `/reset` with a plain GET link, which a browser prefetch or a link scanner can fire. Not copying that costs a few lines of HTML. Changing `/reset` itself is out of scope.                                                                                          |

### Implementation shape

- One self-built `WebApplication` in `FallbackHostBuilder` serves both statuses, which is what makes both of them reach the extended surface.
  It must stay self-built: of the two call sites in `WebApplicationBuilderExtensions`, `RunInvalidOptionsHost` runs *after* `builder.Build()`, where the service collection is sealed.
- The page is a `.razor` component in `Core.OS` rendered in **static SSR** via `AddRazorComponents()` and `RazorComponentResult<T>`, which needs no new package reference: `Core.OS` is `Microsoft.NET.Sdk.Web` and `Microsoft.AspNetCore.Components.Endpoints` is part of the shared framework.
  The component renders the whole document including an inline `<style>` block, so no shell or layout infrastructure is needed.
  The `.razor` file does make the Web SDK infer `RequiresAspNetWebAssets`, which pulls `Microsoft.AspNetCore.App.Internal.Assets` in implicitly for `blazor.web.js`.
  Static SSR never loads that script, and an implicit reference cannot carry the `PackageVersion` that central package management pins for it (NU1009), so `Core.OS.csproj` sets the property to `false`.
- Every group is resolved into a plain view-model record **before** rendering, each one either resolved or marked unavailable.
  No file access happens inside the component.
- The action is `POST /env-overrides/disable`, which renames the file, logs at warning level the way `DowngradeReset` does, and restarts through `IPipeClient.RestartSuite` on the delayed-restart pattern of `DowngradeWebApiHostBuilder.RestartDelayed`.

## Consequences

### Positive

- An operator recovers an instance bricked by a fatal override value from a browser, without shell access.
- Both failsafe statuses gain the grouped surface from one implementation.
- Renaming aside keeps the failing configuration readable for diagnosis.
- Static SSR keeps the page maintainable as markup instead of an interpolated HTML string, with no new dependency.

### Negative

- `/` is no longer machine-readable. Anything that wants to poll the failsafe state must use `/health`.
- The page duplicates a small amount of styling rather than reusing the `Blazor.Shared` design system, which expects the UI host services.
- An instance that legitimately needs its overrides can be stripped of them by anyone who can reach the port while it is in failsafe mode.

### Risks

- **A dead failsafe host is a restart loop.** An exception during SSR becomes a 500, and under `Restart=always` the operator never sees the page. Mitigated by resolving the view model ahead of rendering and covered by a test with an invalid `HomeDirectory`.
- **The action is unauthenticated**, like the downgrade page. Renaming aside instead of deleting keeps the worst case recoverable.
- **Out of scope, worth knowing:** the downgrade page's `/reset` GET link wipes all data and has the same prefetch exposure.

## Compliance

- `FallbackHostBuilderTests` assert `text/html`, both statuses, the unchanged 500 / 503 status codes, and `/health` still unhealthy with the joined description.
- A test with an invalid `InstanceOptions.HomeDirectory` asserts the page renders the affected groups as unavailable instead of throwing.
- `MockFileSystem` cases cover the env-overrides group with the switch off, the file missing, present and malformed.
- The action is covered against `HostManagementOptions.MockClient`, asserting the rename, the log line and the restart request.
- Any new group added to the surface must be resolvable without DI and must be reviewed against D3 before it renders a value.
