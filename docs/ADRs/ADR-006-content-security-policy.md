# ADR-006: Content Security Policy

## Status

Accepted

## Date

2026-09-07

## Context

CSP is the browser-side allowlist that decides which origins a page may load code, styles, images and connections from, and it is the standard defence in depth against XSS and data exfiltration once an injection already exists ([MDN guide](https://developer.mozilla.org/en-US/docs/Web/HTTP/Guides/CSP), [OWASP CSP Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Content_Security_Policy_Cheat_Sheet.html)).
The Suite sends a [`Content-Security-Policy`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy) header on every dynamic response.

This ADR answers [suite#2885](https://gitlab.com/vicione-oss/vicione/suite/suite/-/work_items/2885) and the findings it links: [#512](https://gitlab.com/vicione-oss/issue-defect-management/-/work_items/512) (missing directive without fallback), [#513](https://gitlab.com/vicione-oss/issue-defect-management/-/work_items/513) (wildcard-equivalent directives), [#514](https://gitlab.com/vicione-oss/issue-defect-management/-/work_items/514) (`script-src` `'unsafe-inline'`), [#515](https://gitlab.com/vicione-oss/issue-defect-management/-/work_items/515) (`style-src` `'unsafe-inline'`), [#531](https://gitlab.com/vicione-oss/issue-defect-management/-/work_items/531) (missing `X-Frame-Options`).

Scope is the Suite (Blazor Web App, `InteractiveServer`, no WebAssembly), the `ViciOne.Ui.Blazor.Components` library, and the nginx reverse proxy shipped by `deb-packaging`.

Five facts drive the decisions.

**The app is the only layer present in every deployment.**
nginx ships with the deb package, but installations also run Kestrel directly or sit behind a customer-managed proxy we can neither configure nor inspect.

**The Suite's own scripts allow a strict script policy.**
No inline `<script>` block and no inline event handler attribute exists in the Suite's own code or in the component library: every script tag has a same-origin `src`, and `Blazor.start()` lives in `suite.js`.
[CSP Level 3](https://www.w3.org/TR/CSP3/) splits `script-src` into [`script-src-elem`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/script-src-elem) for `<script>` elements and [`script-src-attr`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/script-src-attr) for inline event handler attributes, so these can be `'self'` and `'none'`.

**Inline style attributes cannot be avoided.**
The component library passes runtime values into CSS custom properties through 8 inline `style` attributes, and the framework's [`Virtualize`](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/virtualization?view=aspnetcore-10.0) spacers render `style="height: …px; flex-shrink: 0;"` until .NET 11 ([suite#2887](https://gitlab.com/vicione-oss/vicione/suite/suite/-/work_items/2887)).
The values change per render, so hashes cannot cover them, and [`style-src-attr`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/style-src-attr) needs `'unsafe-inline'`.
Inline `<style>` blocks exist only on the failsafe and the downgrade page; their CSS is fixed, so a hash covers it and `style-src` can stay `'self'`.

**Nothing in the browser connects to a foreign host.**
Even the MQTT viewer connects to its broker on the server, through MQTTnet, where no browser policy applies.

**Modules and the TreeEditor rely on what a strict policy blocks.**
[suite#2899](https://gitlab.com/vicione-oss/vicione/suite/suite/-/work_items/2899) found features in cluster-editor, cluster-mgmt and data-collection-wizard that break under it.
The TreeEditor, which the Suite's MQTT filter uses too, needs inline event handlers for drag and drop ([tree-editor#247](https://gitlab.com/vicione-oss/vicione/ui-libs/tree-editor/-/work_items/247)).

## Options Considered

### Option A: No CSP

- **Pros:** nothing to build.
- **Cons:** no clickjacking, exfiltration, `base-uri` or XSS defence in depth; the linked findings stay open.

### Option B: Static same-origin policy in nginx

- One fixed header in `src/nginx/header`.
- **Pros:** no application code.
- **Cons:** absent in deployments without our nginx; breaks the inline `<style>` and `<script>` of the `src/html/unavailable.html` error page.

### Option C: Fail-closed policy in the app (chosen)

- Middleware sends the policy; nginx adds no CSP of its own.
- **Pros:** present in every deployment; the failsafe hosts can carry a policy of their own; the violation endpoint has to live there anyway.
- **Cons:** application code, and a breaking change for modules that load off-origin resources.

### Option D: Nonce plus [`'strict-dynamic'`](https://www.w3.org/TR/CSP3/#strict-dynamic-usage)

- A per-document nonce marks our own script tags as trusted, and `'strict-dynamic'` passes that trust on to the scripts they insert, whatever their origin.
- **Pros:** the only way to keep `IJsInterop.IncludeScript`'s inline content and arbitrary module script origins working under a strict policy ([web.dev](https://web.dev/articles/strict-csp)).
- **Cons:** gives up origin control for scripts; does not cover event handler attributes; the nonce must reach every script tag, enhanced-navigation fragments included, and one missed tag breaks the page.
  Deferred until the module-governance answers exist.

## Decision

Adopt **Option C**: the Suite sends the policy, [enforcing rather than report-only](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy-Report-Only).

The goal is the **target policy** below.
The Suite cannot send it yet, because modules and the TreeEditor would lose features (see *Context*).
Until they are fixed, the Suite sends the **interim policy**: the target policy plus the relaxations listed below.
Each relaxation is removed once its blockers are fixed and released ([suite#2897](https://gitlab.com/vicione-oss/vicione/suite/suite/-/work_items/2897)).

### Target policy

| Directive | Value | Rationale |
|-----------|-------|-----------|
| `default-src` | `'self'` | floor for anything not listed, so no directive falls back to "unrestricted" (#512) |
| `base-uri` | `'self'` | Blazor needs `<base href="/">`; this blocks base-tag injection |
| `script-src` | `'self'` | fallback for browsers without CSP Level 3, and it governs `eval` |
| `script-src-elem` | `'self'` | no inline script block exists |
| `script-src-attr` | `'none'` | no inline event handler attribute exists (#514) |
| `style-src` | `'self'` | no inline `<style>` block on the main host (#515) |
| `style-src-attr` | `'unsafe-inline'` | permanent, see *Context* |
| `img-src` | `'self'` | every image the Suite renders is same-origin, the icons included |
| `font-src` | `'self'` | local woff2 fonts only |
| [`connect-src`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/connect-src) | `'self'` | the SignalR circuit and chunked upload are same-origin; in CSP Level 3, `'self'` also matches the `wss:` URL of the page's own origin |
| `form-action` | `'self'` **plus the configured OpenID provider** | see *`form-action` and the external identity provider* |
| `frame-src` | `'none'` | no iframes |
| `worker-src` | `'none'` | no web workers |
| `object-src` | `'none'` | no `<object>` / `<embed>` |
| `frame-ancestors` | `'none'` | **the Suite is never embedded in another site** (#531) |
| `upgrade-insecure-requests` | set | the Suite is reached over HTTPS only (`UseHttpsRedirection`, `UseHsts`) |
| `report-uri` | `/csp-report` | for browsers that predate `report-to` |
| [`report-to`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/report-to) | `csp-endpoint` | the same endpoint, named through the [`Reporting-Endpoints`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Reporting-Endpoints) header; a browser that supports it ignores `report-uri` |

No directive carries a scheme-wide value (#513).
The fallbacks `script-src` and `style-src` carry no `'unsafe-inline'`, so the policy fails closed in older browsers (see *Browser baseline*).

### Interim policy

This is the policy the Suite sends today, from `ContentSecurityPolicy.GetBaseline`.
It is the target policy with these relaxations.
The finding ids (CE-…, CM-…, DCW-…, DX-…) refer to [suite#2899](https://gitlab.com/vicione-oss/vicione/suite/suite/-/work_items/2899).

| Relaxation | Keeps working | Blocked by |
|------------|---------------|------------|
| `script-src 'unsafe-eval'` | tree node tooltips in cluster-editor | CE-1 |
| `script-src-elem 'unsafe-inline'` | package upload in cluster-mgmt | CM-1 |
| `script-src-attr 'unsafe-inline'` | drag and drop in every TreeEditor, the Suite's MQTT filter included, and onto cluster-editor connectors | [tree-editor#247](https://gitlab.com/vicione-oss/vicione/ui-libs/tree-editor/-/work_items/247), CE-2 |
| `style-src 'unsafe-inline'` | data-collection-wizard dialog layout, `<style>` in cluster-editor labels | DCW-1, CE-5, CE-6, a browser check of DX-B1 |
| `https://maxcdn.bootstrapcdn.com` in `style-src` and `font-src` | toolbar icons of the cluster-editor label editor | CE-4 |
| `img-src data:` | context menu icons, IODD device pictures, dropdown arrows, `data:` images in labels | CE-3, CE-5, CE-6, CE-7, CM-2, DCW-2, DCW-3 |
| `img-src https:` | outside images in cluster-editor labels | CE-5, CE-6 |
| `connect-src wss:` | nothing known | the browser checks in suite#2897 |

### `form-action` and the external identity provider

A browser enforces `form-action` on every hop of a form submission, redirects included.
The external sign-in and account-linking endpoints answer with a redirect to the provider, so `'self'` alone refuses them.
The directive therefore also names the origin of the configured provider's authority, read from the same `OpenIdConnectOptions` the challenge is built from, so it follows a provider change without a restart.
Without a configured provider it is exactly `'self'`.
A policy for just these endpoints would not help, because `form-action` is checked against the document that holds the form, and the account-linking button sits in the settings dialog, which opens over any page.

- **What it costs:** injected script can post a form to the provider's origin, an exfiltration channel to one host the administrator chose.
- **What it does not cover:** the redirect goes to the discovery document's `authorization_endpoint`, which every provider we know of serves from the authority's origin; a provider that does not stays blocked.
- **A blocked hop is hard to diagnose**, because the browser reports the URL before the redirect, so `/csp-report` does not name the provider.

### Embedding

`frame-ancestors 'none'` is a decision, not an assumption: the Suite is not embedded anywhere, and we do not intend to support it.
[`X-Frame-Options: DENY`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/X-Frame-Options) is sent with it as the legacy equivalent, as [OWASP recommends](https://cheatsheetseries.owasp.org/cheatsheets/Clickjacking_Defense_Cheat_Sheet.html), and the Suite is its only sender, because a browser may ignore a duplicate.

### Violation reporting

The policy reports to `POST /csp-report`, which the Suite serves itself and which logs each violation as a warning, so violations travel with the logs an administrator sends us and nothing leaves the customer network.
Nothing in `IClientModuleResourceProvider` declares a resource origin up front, so this log is the only way to tell which module a blocked origin belongs to.

### Where the policy lives

| Layer | Responsibility |
|-------|----------------|
| Suite (middleware) | the policy and `X-Frame-Options`; the only layer present in every deployment |
| Suite (failsafe and downgrade hosts) | a stricter policy of their own, without the interim relaxations: no scripts, no style attributes, and the inline `<style>` block allowed by a SHA-256 hash computed at runtime over the stylesheet the page renders. No reporting directive, because the app that would answer `/csp-report` is the one that failed to come up |
| nginx (`deb-packaging`) | HSTS, `X-Content-Type-Options` and `Referrer-Policy`; no CSP and no `X-Frame-Options` |

The policy does not vary by environment, only `form-action` varies per instance, so E2E tests the header customers get.
The details per layer are in [Security Headers](../security-and-identity.md#security-headers).

## Browser baseline

The policy requires a CSP Level 3 browser: Firefox 108, Safari and iOS Safari 15.4, Chrome 75, Edge 79.
Older browsers ignore the split directives and apply the `script-src` and `style-src` fallbacks instead, and parts of the UI break.
Under the target policy this hits the component library's inline style attributes; under the interim policy it hits inline event handlers, such as TreeEditor drag and drop.
So the floor decides whether the Suite works, not how much protection it gets.
This ADR does not set a product-wide browser policy.

## What needs to be done

| # | Repo | Work |
|---|------|------|
| 1 | `suite` | Remove each interim relaxation once its blockers are released ([suite#2897](https://gitlab.com/vicione-oss/vicione/suite/suite/-/work_items/2897)) |
| 2 | `suite` | Drop `report-uri` once the supported floor no longer includes browsers without `report-to` |
| 3 | `suite` | Confirm the `blob:` download by hand on the floor browsers (Firefox 108, Safari 15.4); Chromium is covered by E2E |
| 4 | `blazor-components` | Raise the 8 inline `style` attributes with the maintainers. Low priority — `Virtualize` needs `style-src-attr 'unsafe-inline'` until .NET 11 anyway |
| 5 | `suite-sdk` | Module client reach as an SDK contract question, see *Risks* — a separate ADR |

## Consequences

### Positive

- Clickjacking and `base-uri` protection in every deployment, including those without our nginx, already under the interim policy.
- Both policies block off-origin scripts, frames, workers and plugins.
- The target policy also blocks inline scripts, inline event handlers and off-origin connections from script, and it answers all five linked findings.
- Module breakage shows up in the administrator's logs instead of staying silent.

### Negative

- `style-src-attr 'unsafe-inline'` stays for the foreseeable future. CSS injection is the narrower risk, so this is accepted.
- The policy is a constant with no switch to relax or disable it. When a module with wanted functionality breaks, the directive for that resource is relaxed in the policy itself, as the interim policy does; a configurable policy is considered only if that turns out not to be good enough.
- `connect-src 'self'` rests on an assumption about modules that the Suite does not enforce; a module that needs more requires an ADR change and a release.
- `form-action` varies per instance: with a configured provider it permits form posts to that one host.
- Older browsers lose parts of the UI, see *Browser baseline*.

### Risks

- **The interim policy is much weaker than the target.** It allows inline scripts, inline styles and scheme-wide sources, so it leaves #513, #514 and #515 open: every place that renders unescaped text as HTML is a working script injection, and injected script can open a WebSocket to any host. suite#2899 lists the known places and rates the threats.
- **Every relaxation that is removed is a breaking change** for a module that started to rely on it.
- **The policy is a breaking change for modules.** Under the target policy, a module stops working if it loads from a CDN (`IClientModuleResourceProvider`'s `Resource.Url` is an unrestricted `Uri` carrying `Integrity` and `CrossOrigin`), posts via `SubmitForm` to a foreign action, uploads via `UploadFiles` to a foreign URL, opens a WebSocket from the browser, or embeds an iframe. The interim policy still allows some of these. We cannot tell from the outside which installed modules are affected — the violation log turns that into evidence.
- Keeping `IncludeScript`'s inline-content path and arbitrary resource origins as SDK capabilities means eventually taking Option D and giving up origin control for scripts. Dropping them is what makes `script-src-elem 'self'` permanent. That decision, and whether dropping off-origin support is a breaking SDK change, are module governance and are **not decided here**.
- [`require-trusted-types-for 'script'`](https://developer.mozilla.org/en-US/docs/Web/API/Trusted_Types_API) is the only mechanism that constrains what module JavaScript does to the DOM. It is not in the target policy: Firefox supports it only from 148, and its fit with Blazor's `MarkupString` diffing and `IncludeScript`'s `innerHTML` write is unresolved, also in [Microsoft's CSP guidance for Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/content-security-policy?view=aspnetcore-10.0). It needs a report-only spike first.
- [`Referrer-Policy`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Referrer-Policy) is at `no-referrer-when-downgrade`, the weakest useful value, and `Permissions-Policy`, `Cross-Origin-Opener-Policy` and `Cross-Origin-Resource-Policy` are unset. Same code paths, deliberately a second round.

## Compliance

- The header, the failsafe and downgrade policies with their runtime hashes, the violation endpoint and the `blob:` download are asserted by integration tests and by E2E, with the policy enforcing. E2E pins the exact policy the Suite sends, which is the interim one today.
- `deb-packaging` runs no tests and no E2E job serves the Suite through nginx, so a change to `src/nginx/header` is verified by review and on an installed package.
- The number of inline `style` attributes is not policed, because `style-src-attr 'unsafe-inline'` is permanent.
- No new inline event handler and no new inline `<script>` block: the target policy refuses them, so one added now breaks when its relaxation is removed.
- MRs adding an inline `<style>`, a `blob:` or `data:` consumer, a WebSocket opened from the browser, or a new off-origin fetch must state the directive they need under the target policy.
- The `form-action` origin is unit-tested, including a provider change without a restart. E2E pins the policy without a provider.
- A value rendered as raw HTML (`MarkupString`, `innerHTML`) is either encoded before it becomes markup, as the instance title is, or trusted by its origin — the `TopBar` icon and the failsafe page's own stylesheet are the only trusted cases, and request, database or module input never joins them.
- New SDK client APIs that reach the DOM or the network are reviewed against this ADR before they become public.
