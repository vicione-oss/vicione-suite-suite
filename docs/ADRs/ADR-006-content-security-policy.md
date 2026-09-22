# ADR-006: Content Security Policy

## Status

Accepted

## Date

2026-09-07

## Context

CSP is the browser-side allowlist that decides which origins a page may load code, styles, images and connections from, and it is the standard defence in depth against XSS and data exfiltration once an injection already exists ([MDN guide](https://developer.mozilla.org/en-US/docs/Web/HTTP/Guides/CSP), [OWASP CSP Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Content_Security_Policy_Cheat_Sheet.html)).
The Suite sends the [`Content-Security-Policy`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy) header below on every dynamic response.
A second one, `frame-ancestors 'self'`, comes from the framework because `AddInteractiveServerRenderMode` enables WebSocket compression; a browser enforces every policy it receives, so what applies is the intersection of the two.

This ADR answers [suite#2885](https://gitlab.com/vicione-oss/vicione/suite/suite/-/work_items/2885) and the findings it links: [#512](https://gitlab.com/vicione-oss/issue-defect-management/-/work_items/512) (missing directive without fallback), [#513](https://gitlab.com/vicione-oss/issue-defect-management/-/work_items/513) (wildcard-equivalent directives), [#514](https://gitlab.com/vicione-oss/issue-defect-management/-/work_items/514) (`script-src` `'unsafe-inline'`), [#515](https://gitlab.com/vicione-oss/issue-defect-management/-/work_items/515) (`style-src` `'unsafe-inline'`), [#531](https://gitlab.com/vicione-oss/issue-defect-management/-/work_items/531) (missing `X-Frame-Options`).

Scope is the Suite (Blazor Web App, `InteractiveServer`, no WebAssembly), the `ViciOne.Ui.Blazor.Components` library, and the nginx reverse proxy shipped by `deb-packaging`.

Four properties of the current code drive the decisions.

**The app is the only layer present in every deployment.**
nginx ships with the deb package, but installations also run Kestrel directly or sit behind a customer-managed proxy we can neither configure nor inspect.
A policy living only in `deb-packaging/src/nginx/header` is absent exactly where we control the environment least.

**A strict script policy is free today.**
No inline `<script>` block and no inline event handler attribute exists in the Suite or in the component library: every script tag has a same-origin `src`, and `Blazor.start()` lives in `suite.js`.
CSP Level 3 splits `script-src` into [`script-src-elem`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/script-src-elem) for `<script>` elements and [`script-src-attr`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/script-src-attr) for inline event handler attributes ([CSP Level 3](https://www.w3.org/TR/CSP3/)), so both can be `'self'` and `'none'`.

**A strict style policy is not achievable.**
The component library passes runtime values into CSS custom properties through 8 inline `style` attributes, and the framework's [`Virtualize`](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/virtualization?view=aspnetcore-10.0) spacers still render `style="height: …px; flex-shrink: 0;"` on .NET 10, fixed in .NET 11 and tracked in [suite#2887](https://gitlab.com/vicione-oss/vicione/suite/suite/-/work_items/2887).
The values change per render, so hashes cannot cover them, and either source alone forces [`style-src-attr`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/style-src-attr) `'unsafe-inline'`.
Inline `<style>` **blocks** are a different directive and a different situation: the only two in the code are the failsafe and the downgrade page, whose CSS is fixed and can therefore be allowed by a hash, so `style-src` itself stays `'self'` everywhere.

**Modules connect to the message queue from the browser**, on hosts that are neither the page origin nor knowable when the policy is written.
Broker endpoints are runtime records rather than configuration: the configured endpoint is only seeded into the `Connections` table at startup, and administrators and modules create, edit and delete further connections afterwards, which `IConnectionService` hands to client modules without a restart.
[`connect-src`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/connect-src) is the only directive governing WebSocket connections and has no WebSocket-specific counterpart, so it can be neither same-origin nor an enumeration of known hosts.

## Options Considered

### Option A: No CSP

- **Pros:** nothing to build.
- **Cons:** no clickjacking, exfiltration, `base-uri` or XSS defence in depth; the linked findings stay open.

### Option B: Static same-origin baseline in nginx

- One fixed header set in `src/nginx/header`.
- **Pros:** no application code.
- **Cons:** absent in deployments without our nginx; cannot distinguish environments; a server-level policy would break the inline `<style>` and `<script>` in the `src/html/unavailable.html` error page.

### Option C: Fail-closed baseline in the app (chosen)

- Middleware sends the policy; nginx adds no CSP of its own.
- **Pros:** present in every deployment; the failsafe hosts can carry a policy of their own; the violation endpoint has to live there anyway.
- **Cons:** application code, and a breaking change for modules that load off-origin resources.

### Option D: Nonce plus [`'strict-dynamic'`](https://www.w3.org/TR/CSP3/#strict-dynamic-usage)

- A per-document nonce marks our own script tags trusted, and `'strict-dynamic'` propagates that trust to any `<script>` those scripts insert, regardless of origin.
- **Pros:** the only route that keeps `IJsInterop.IncludeScript`'s inline content and arbitrary module resource origins working under a strict policy, and the configuration [web.dev's strict-CSP guidance](https://web.dev/articles/strict-csp) recommends when the script set is not known at build time.
- **Cons:** `'strict-dynamic'` makes host allowlists inert for scripts, so it trades origin control for inline coverage; a nonce does not cover event handler attributes, so it buys nothing for `script-src-attr`; the nonce must be plumbed through `App.razor`, `suite.js` and every server-rendered enhanced-navigation fragment, where one missed tag is a broken page rather than a warning.
  Deferred until the module-governance answers exist.

## Decision

Adopt **Option C**.
The header is emitted by the Suite, [enforcing rather than report-only](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy-Report-Only), and it fails closed: no `'unsafe-inline'` in the fallback `script-src` / `style-src` as a safety net for pre-CSP3 browsers.
The alternative would leave an unknown share of clients silently running with no protection, which we could not detect from the outside.

### Baseline

| Directive | Value | Rationale |
|-----------|-------|-----------|
| `default-src` | `'self'` | floor for anything not listed, so no directive falls back to "unrestricted" (answers #512) |
| `base-uri` | `'self'` | Blazor needs `<base href="/">`; this blocks base-tag injection |
| `script-src` | `'self'` | fallback list, and it still governs the `eval` sink check |
| `script-src-elem` | `'self'` | no inline script block exists, so this is free and blocks injected `<script>` from day one |
| `script-src-attr` | `'none'` | no inline event handler attribute is rendered anywhere (answers #514) |
| `style-src` | `'self'` | no inline `<style>` block on the main host; the failsafe and downgrade pages carry one and it is covered by a hash (#515) |
| `style-src-attr` | `'unsafe-inline'` | permanent; component library and `Virtualize`, all values dynamic so hashes are not an option |
| `img-src` | `'self'` | every image the Suite renders is same-origin, the icons included. QuickGrid's own CSS draws the `Paginator` and `ColumnOptions` controls from `data:` images, so adding either means asking for `data:` |
| `font-src` | `'self'` | local `noto-sans` woff2 only |
| `connect-src` | **`'self' wss:`** | SignalR circuit and chunked upload are same-origin; module broker connections are not and cannot be enumerated, see below |
| `form-action` | `'self'` **plus the configured OpenID provider** | Identity and passkey endpoints are same-origin; the external sign-in and account-linking endpoints answer with a redirect to the provider, and a browser enforces this directive on every hop of a submission, see below |
| `frame-src` | `'none'` | no iframes |
| `worker-src` | `'none'` | no web workers |
| `object-src` | `'none'` | no `<object>` / `<embed>` |
| `frame-ancestors` | `'none'` | **the Suite is never embedded in another site** (#531) |
| `upgrade-insecure-requests` | set | unconditionally: `UseHttpsRedirection` and `UseHsts` already rule out reaching the Suite over plain HTTP, so a switch would only weaken the policy |
| `report-uri` | `/csp-report` | the local violation endpoint, for browsers at the floor that predate its successor |
| [`report-to`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/report-to) | `csp-endpoint` | the same endpoint, named through the [`Reporting-Endpoints`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Reporting-Endpoints) response header; a browser supporting it ignores `report-uri` |

`connect-src` is the only scheme-wide value in the baseline; every other directive names `'self'`, `'none'` or a single scheme we control, so #513 is answered everywhere but there.

### `connect-src` and the message queue

Modules must be able to open a WebSocket to a broker directly, on a host that is not the page origin and cannot be enumerated when the policy is built, for the reason given in *Context*.
A `connect-src` assembled from configuration would name one origin and block every other broker the Suite's own connection management handed out — the Suite breaking its own feature.
`connect-src` is therefore **`'self' wss:`**, a deliberate exception to the rule the rest of the table follows.

- **What it costs:** injected script can open a WebSocket to any host and stream data out. That is the exfiltration channel a named-origin `connect-src` would have closed.
- **What still holds:** `wss:` is a scheme, not a wildcard. Off-origin `fetch`, `XMLHttpRequest`, `EventSource` and `sendBeacon` stay blocked by `'self'`, so the plain-HTTP exfiltration route stays shut.
- `ws:` is not listed, so a broker reachable only over plaintext fails to connect. That is intended — the fix is TLS on the broker, not a relaxed policy.
- Narrowing this is a work item of its own, see *What needs to be done*.

### `form-action` and the external identity provider

An earlier version of the table above read *"an OIDC challenge that stays a `302` redirect needs nothing more"*.
That is wrong.
A browser enforces `form-action` on **every hop of a form submission**, the redirect included, so the challenge that the external sign-in and account-linking endpoints answer with is refused by `'self'` alone.
The symptom is a console message naming the Suite's own same-origin endpoint as the blocked URL, because Chromium reports the form's action rather than the redirect target it actually refused.

The directive therefore carries the origin of the configured provider, derived from its authority.
That value is read from the same `OpenIdConnectOptions` the challenge itself is built from, so the policy cannot name a provider the redirect does not go to, and it follows the `IOptionsMonitorCache` clear that `ExternalIdProviderChanged` triggers on every node — an administrator changing the authority in the settings panel gets a correct policy without a restart.
An instance with no provider configured sends exactly `form-action 'self'`.

Two alternatives were rejected.

- **Turning the two endpoints into `GET` navigations**, which `form-action` does not govern at all.
  It removes antiforgery protection from account linking, and link-CSRF is account-takeover-shaped: an attacker who can start a linking flow in a victim's browser attaches their own external identity to the victim's account.
- **Scoping the relaxation to the documents that hold the forms.**
  `form-action` is evaluated against the policy of the document containing the form, not against the response of the submission, so a per-endpoint header would change nothing.
  The sign-in form sits on one known route, but the linking button lives in a control panel of the global settings dialog, which opens over any page without a navigation — there is no route to attach a wider policy to.

- **What it costs:** injected script can post a form to the provider's origin.
  The provider rejects it, but the request body leaves the origin, so this is an exfiltration channel of one administrator-chosen host — narrower than the `connect-src wss:` above, which permits a WebSocket to any host at all.
- **What it does not cost:** the endpoints stay `POST` behind their antiforgery tokens.
- **What it does not cover:** the redirect strictly goes to the discovery document's `authorization_endpoint`, which every provider we know of serves from the authority's own origin.
  A provider that splits the two stays blocked, see *What needs to be done*.
  Reading the document instead would be exact, at the price of a network call on the path that writes a response header.
- **A blocked hop is hard to diagnose**, because the violation a browser reports names the pre-redirect URL rather than the origin it refused.
  `/csp-report` will therefore not name the provider.

### Embedding

`frame-ancestors 'none'` is a decision, not an assumption: the Suite is not embedded anywhere, and we do not intend to support it.
[`X-Frame-Options: DENY`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/X-Frame-Options) is sent alongside it as the legacy equivalent, which is the layering [OWASP recommends](https://cheatsheetseries.owasp.org/cheatsheets/Clickjacking_Defense_Cheat_Sheet.html), and the Suite is its only sender so that no duplicate of it reaches the browser.

### Violation reporting

The policy reports to a **local** endpoint the Suite serves itself, `POST /csp-report`, which logs each incident as a warning, so violations travel with the logs an administrator sends us and nothing leaves the customer network.
A federated collector, and the data-protection question it would raise, stays out of scope.
This is what makes module breakage diagnosable: nothing in `IClientModuleResourceProvider` declares a resource origin up front, so the log is the only way an administrator can tell us which module a blocked origin belongs to.

### Where the policy lives

| Layer | Responsibility |
|-------|----------------|
| Suite (middleware) | the policy and `X-Frame-Options`; the primary and only guaranteed home. Identical in every environment but for the configured provider's origin in `form-action`, which is a property of the instance |
| Suite (failsafe and downgrade hosts) | their own, stricter policy: `script-src 'none'`, `style-src-attr 'none'` and `connect-src 'self'`, since neither page carries a script or a style attribute or talks to a broker, and their inline `<style>` block is allowed by a SHA-256 hashed at runtime over the stylesheet the page renders, so the two cannot drift apart. Neither carries a reporting directive — the app that would answer `/csp-report` is the one that failed to come up |
| nginx (`deb-packaging`) | HSTS, `X-Content-Type-Options` and `Referrer-Policy`, the headers it still sends. No CSP and no `X-Frame-Options` |

The policy does not vary by environment.
There is no Development variant to keep in step with the production one, so E2E exercises the header customers get, **enforcing**.
What does vary is the provider origin in `form-action`, and that varies per instance rather than per environment; the instance under test configures no provider, so E2E pins the unwidened policy.
The layer-by-layer detail, including what the packaged nginx must not send, is in [Security Headers](../security-and-identity.md#security-headers).

## Browser baseline

This policy makes a CSP Level 3 browser the baseline for the Suite.
The compatibility tables put that floor at Firefox 108, Safari and iOS Safari 15.4, Chrome 75 and Edge 79, with Firefox and Safari the binding constraint.

Below the floor a browser ignores the split directives and enforces the `script-src` / `style-src` fallbacks instead.
Scripts are unaffected, because every script the Suite runs has a same-origin `src`; the component library's inline `style` attributes, however, are then governed by `style-src 'self'` and break visibly.
So the floor decides whether the application works at all, not how much protection it gets.

The Suite has no documented browser requirement today, and other features may well impose floors of their own.
This ADR records the CSP requirement only; it does not set a product-wide browser policy.

## What needs to be done

| # | Repo | Work |
|---|------|------|
| 1 | `suite` | Drop `report-uri` once the supported floor rises past the browsers that predate `report-to`, leaving one reporting directive instead of two |
| 2 | `suite` | Confirm the `blob:` download by hand on the floor browsers (Firefox 108, Safari 15.4); Chromium is covered by E2E |
| 3 | `suite` | Narrow `connect-src` from `wss:` towards named origins. The candidates are an explicitly configured origin list and reading the `Connections` table with the SDK's change event as the invalidation hook; the violation log decides whether the second is worth its cost. Either way a connection added while a page is open needs a reload, because a CSP is fixed at document load |
| 4 | `blazor-components` | Raise the 8 inline `style` attributes with the maintainers. Low priority — `Virtualize` requires `style-src-attr 'unsafe-inline'` until .NET 11 regardless |
| 5 | `suite-sdk` | Module client reach as an SDK contract question, see *Risks* — a separate ADR |
| 6 | `suite` | Derive the `form-action` origin from the discovery document's `authorization_endpoint` rather than from the authority, once a provider turns up that serves the two from different origins. The cheap intermediate step is an explicitly configured list of extra origins; the resolution itself belongs on the `ExternalIdProviderChanged` handler, never on the path that writes a response header |

## Consequences

### Positive

- Clickjacking protection, `base-uri` protection, and no off-origin exfiltration over HTTP, in every deployment, including those without our nginx.
- An injected `<script>` element, an injected script block and an injected `<script src="https://attacker/">` are all blocked by `script-src-elem 'self'`, with no code change. What the policy does not cover is closed in the markup itself: request- and database-fed values are encoded before they become raw HTML, and no inline event handler attribute is left to execute.
- Four of the five linked findings are answered by the baseline table; #513 is answered in every directive except `connect-src`.
- Module breakage becomes visible to the administrator instead of silent.

### Negative

- `style-src-attr 'unsafe-inline'` stays for the foreseeable future. CSS injection is the narrower risk, so this is accepted.
- The policy is a constant with no switch to relax or disable it. If a module with wanted functionality breaks, the answer is to relax the directive for that resource in the baseline; making the policy configurable is reconsidered only if the baseline itself turns out not to be good enough.
- `connect-src wss:` leaves WebSocket exfiltration open to any host. It buys module broker connections that no configuration can enumerate, and item 3 is what narrows it.
- `form-action` is no longer one constant: an instance with a configured provider permits form posts to that one host. It is narrower than the `connect-src` exception, and the alternative was dropping antiforgery from account linking.
- On a pre-CSP3 browser the component library's inline styles break visibly, see *Browser baseline*.
- A browser's own report delivery is not observable from the test harness, because the Reporting API uploads out of band and never becomes a request Playwright can see. What E2E holds is that the Suite advertises the route and answers the payload posted to it.

### Risks

- **This is a breaking change for modules.** A module loading from a CDN (`IClientModuleResourceProvider`'s `Resource.Url` is an unrestricted `Uri` carrying `Integrity` and `CrossOrigin`), posting via `SubmitForm` to a foreign action, uploading via `UploadFiles` to a foreign URL, or embedding an iframe stops working the moment the header is set. We cannot tell from the outside which installed modules are affected — the violation log is what turns that into evidence.
- Keeping `IncludeScript`'s inline-content path and arbitrary resource origins as SDK capabilities means eventually taking Option D and giving up origin control for scripts. Dropping them is what makes `script-src-elem 'self'` permanent. That decision, the per-module escape-hatch question, and whether removing off-origin support is a breaking SDK change needing a major version and a deprecation window, are module governance and are **not decided here**.
- [`require-trusted-types-for 'script'`](https://developer.mozilla.org/en-US/docs/Web/API/Trusted_Types_API) is the only mechanism that constrains what module JavaScript may do to the DOM rather than only where it may load code from. It is not in the baseline: Firefox support arrived only in 148, and it has an unresolved compatibility question with Blazor's raw-HTML diffing for `MarkupString` and with the `innerHTML` write in `IncludeScript`'s inline-content path, which [Microsoft's CSP guidance for Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/content-security-policy?view=aspnetcore-10.0) does not address either. It needs a report-only spike first.
- The policy already names the provider in `form-action`, so it is per-instance. Going further would widen three more directives to provider hosts: showing IdP logos or the `picture` claim (`img-src`), switching the challenge to `OpenIdConnectRedirectBehavior.FormPost`, whose generated auto-submit page carries an inline script (`script-src`), or adopting `check_session_iframe` (`frame-src`). Keeping provider branding local is a cheap guardrail worth committing to.
- A strict-looking policy can still be bypassable through what it permits; `connect-src wss:` and the `'unsafe-inline'` on `style-src-attr` are the parts worth re-reading against [known bypass classes](https://portswigger.net/web-security/cross-site-scripting/content-security-policy) whenever they change.
- [`Referrer-Policy`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Referrer-Policy) is at `no-referrer-when-downgrade`, the weakest useful value, and `Permissions-Policy`, `Cross-Origin-Opener-Policy` and `Cross-Origin-Resource-Policy` are unset. Same code paths, deliberately a second round.

## Compliance

- The header, the failsafe and downgrade policies with their runtime hashes, the violation endpoint and the `blob:` download are all asserted by integration tests and by E2E against the instance under test, with the policy enforcing, so a regression fails a test rather than reaching a customer.
- The nginx fragments are the exception: `deb-packaging` runs no tests and no E2E job serves the Suite through nginx, so a change to `src/nginx/header` is verified by review and on an installed package.
- `style-src-attr 'unsafe-inline'` is permanent, so the number of inline `style` attributes is not policed — a count would be a maintained list buying no protection. Inline event handlers are the opposite case: `script-src-attr` is `'none'`, so an inline handler added to the markup is a broken control, not a cosmetic slip.
- MRs adding an inline event handler, an inline `<style>`, a `blob:` or `data:` consumer, or a new off-origin fetch, must state the directive they need.
- The `form-action` origin is unit-tested against the unconfigured sentinel, an authority carrying a path, a non-default port and an authority equal to the Suite's own origin, and against a provider changing without a restart. E2E pins the unwidened policy, because the instance under test configures no provider.
- A value rendered as raw HTML (`MarkupString`, `innerHTML`) is either encoded before it becomes markup, as the instance title is, or trusted by its origin — the `TopBar` icon is the only trusted case, and request, database or module input never joins it.
- New SDK client APIs that reach the DOM or the network are reviewed against this ADR before they become public.
