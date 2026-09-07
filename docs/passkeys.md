## Identity

- Identity needs to be configured to use `Microsoft.AspNetCore.Identity.IdentitySchemaVersions.Version3`
- Identity reads its configuration options (`Microsoft.AspNetCore.Identity.IdentityOptions`) from the application's service provider

## ViciOne specifics

- VO does not use `AddDbContext` to register its DB contexts. Instead it implements the SDK's `Sdk.Backend.Persistence.IModuleDbContextRegistrar` (`Core.OS/Persistence/ModuleDbContextRegistrar`), which picks the provider at runtime — local SQLite or PostgreSQL (master/slave) — and builds the `DbContextOptions` by hand.
- Building the options by hand means the application service provider is not wired up automatically. `AddDbContext` normally calls `UseApplicationServiceProvider` for you; because VO bypasses `AddDbContext`, **the host must call `UseApplicationServiceProvider` itself** — the SDK never does, as it is Identity-agnostic.
  - This link is what lets the Identity `DbContext` read its `IdentityOptions` while building the model — above all `Stores.SchemaVersion = IdentitySchemaVersions.Version3`. Without it Identity finds no options and silently falls back to schema **version 1**, dropping the passkey tables and the v3 column constraints.
  - VO sets it in both paths: at runtime in `ModuleDbContextRegistrar` (linked to the app provider where `AddIdentity` set `Version3`), and at design time in the hand-written `IDesignTimeDbContextFactory` for `UserDbContext` (an isolated provider with Identity configured), so generated migrations target v3 as well.

### edge S

Passkeys require a DNS name to be used when [creating the credentials](https://w3c.github.io/webauthn/#sctn-createCredential):

> NOTE: An effective domain may resolve to a host, which can be represented in various manners, such as domain, ipv4 address, ipv6 address, opaque host, or empty host.
> Only the domain format of host is allowed here. This is for simplification and also is in recognition of various issues with using direct IP address identification in concert with PKI-based security.

In practice this means the suite must be reached over a DNS name: accessed by raw IP the effective domain is an IP rather than a domain, which the browser rejects — so the passkey ceremony fails.

To degrade gracefully instead of failing cryptically, the two passkey-**creation** surfaces are grayed out (with an explanatory tooltip) when the host is an IP literal: the sign-in button on the login page and the **Add** button in the profile passkey settings.
Listing, renaming and deleting are unaffected — they touch no authenticator (see [Managing passkeys](#managing-passkeys-list--rename--delete)) and work over IP.
The rule is "**host is an IP literal**", not "strict FQDN": `localhost` and other single-label DNS names stay enabled, because WebAuthn accepts them and they are needed for local development.
The check lives in `Core.Shared.Passkeys.IPasskeyHostSupport` (`IsPasskeyCapableHost`).

**Detecting the host differs per surface, because they run in separate render contexts (see below):**

- **Login** is static-SSR, so `HttpContext` is available — it reads `HttpContext.Request.Host.Host`.
- **Settings** is interactive Blazor with no reliable `HttpContext`, so it reads the browser origin via `NavigationManager.BaseUri`.

> **Reverse-proxy caveat (edge S / nginx).** The login-side check is only correct if `Request.Host` reflects the *client's* host, not an upstream one. There are two ways a proxy can convey the client host, and the app must end up with one of them:
>
> - **Preserve the `Host` header** — the deployed nginx sets `proxy_set_header Host $host;`, so `Request.Host` is already the client host. This is what the edge S setup relies on, and it works regardless of forwarded-headers handling.
> - **Forward it in `X-Forwarded-Host`** — if a proxy instead overwrites `Host` with the upstream and puts the original host in `X-Forwarded-Host`, the app picks it up through the forwarded-headers middleware (first in the pipeline, `ForwardedHeaders.XForwardedHost` included). By default the header is only honoured from loopback proxies; for a proxy on another host add it to the safelist with `Instance__TrustedProxies__0=<proxy-ip-or-cidr>` (see [oidc.md](oidc.md)).
>
> If neither holds — `Host` rewritten to the upstream **and** `X-Forwarded-Host` not honoured — `Request.Host` is the upstream host and login-side detection is wrong. The settings surface reads the browser origin and is unaffected.

### Relationship to the Microsoft Blazor template

The passkey code is adapted from the standard Blazor template scaffolded with Individual Authentication (`dotnet new blazor -au Individual`), which includes passkey support as of .NET 10. As we understand that template:

- a **single** JS module (`PasskeySubmit.razor.js`) is loaded once with `type="module"` in `App.razor`, right after `blazor.web.js`, and is therefore available to every page;
- a **single** `<passkey-submit>` custom element serves both operations via an `operation` attribute — `Request` on the login page and `Create` on the manage-passkeys page;
- both pages are Blazor static-SSR pages under the same host, and **adding** a passkey is a **navigating form post**: the element calls `navigator.credentials.create`, sets the form value, and submits the form to a server-side page handler. In that template the only passkey-specific endpoints are the two challenge endpoints (creation options, request options) — there is no dedicated "add passkey" endpoint.

### Where ViciOne diverges (and why)

ViciOne keeps the template's sign-in path close to the original, but makes one deliberate product choice that drives every divergence below: **adding a passkey happens inside the interactive Blazor settings dialog**, not on the template's standalone manage-passkeys page.

- **Add/manage passkeys live in the Blazor settings dialog**, as control panels (`AddPasskeyControlPanel`, `PasskeysControlPanel`, `RenamePasskeyControlPanel`) on the SDK `ControlPanelBase` framework. This gives the native look-and-feel and reuses the suite's save/validation/grid/rename/delete machinery, which the stock standalone manage-passkeys page would not integrate with. A navigating form post (the template's approach) would tear down the in-dialog SPA experience, so the add flow stays inside the Blazor app instead: the C# save handler (`AddPasskeyControlPanelSaveHandler`) calls JS interop into `SuitePasskeys.ObtainAndCreateCredentials` — a global that the control panel's colocated module `AddPasskeyControlPanel.razor.js` registers on `window` — which fetch-POSTs the credential to a dedicated **`POST /account/add-passkey`** endpoint in place of the template's form post. All passkey endpoints are feature-gated on `Passkeys`.
- **Sign-in is identified by the user name, not an email.**
  The template registers a user with the email as the Identity `UserName`, so its login page reads its email field and passes that value to the challenge endpoint; ViciOne keeps the two apart (the seeded `Admin` has the email `Admin@it-masters.com`) and its login form offers a user name field only.
  `PasskeySubmit` is therefore pointed at `Input.Username` through its `UsernameField` parameter, and the value is percent-encoded on the way out because `+` is a legal user name character that would otherwise reach the server as a space.
  The identifier only narrows `allowCredentials` — sign-in itself resolves the user from the credential (`PasskeySignInAsync`) — so a wrong or missing one still signs the user in through a discoverable credential, which is why it is covered by asserting the outgoing challenge request (`PasskeyChallengeIdentifierTests`) rather than the sign-in outcome.
- **Sign-in stays on the template's form-post path.** The login page is a Blazor **static-SSR** component (`Login.razor`), not an interactive one: on `/Account` routes `App.razor` resolves the render mode to `null` (`App.razor.cs`), so the login content runs without interactive Blazor — even though `blazor.web.js` and `suite.js` are emitted for every route by `App.razor`. Because a static-SSR page has no interactive component to host a colocated interop module, sign-in keeps the template's self-contained form-associated `<passkey-submit>` custom element, loaded as a classic `<script>` from within `PasskeySubmit.razor`, and uses it for **sign-in (`Request`) only**.

The two surfaces therefore run in **separate JS contexts** — the add flow through a colocated ES module (`AddPasskeyControlPanel.razor.js`) whose helper is exposed on `window` and called via interop inside the interactive Blazor app, and sign-in through a classic, export-nothing custom-element script (`passkey-submit.js`) on the static-SSR login page — which is the root cause of the duplicated helpers below.

### Managing passkeys (list / rename / delete)

Only **creating** a passkey needs the browser (`navigator.credentials.create`), so add is the sole HTTP path (`POST /account/add-passkey`). Listing, renaming, and deleting touch no authenticator — they are server-side operations on the user's stored passkeys, so they run over the suite's in-app message bus (MassTransit, via the SDK's `IUiMediator`), not through any `/account/*` endpoint:

- **List** — `GetPasskeys` → `GetPasskeysResponse` (request/response), handled by `GetPasskeysConsumer` (`UserManager.GetPasskeysAsync`); the grid loads via `Mediator.Request<GetPasskeys, GetPasskeysResponse>`.
- **Rename** — `RenamePasskey` command → `PasskeyRenamingCompleted` event, handled by `RenamePasskeyConsumer` (`UserManager.AddOrUpdatePasskeyAsync`). The save handler sends the command and awaits the completion event, correlated by `CorrelationId`.
- **Delete** — `DeletePasskeys` command → `PasskeyDeletionCompleted` event, handled by `DeletePasskeysConsumer` (`UserManager.RemovePasskeyAsync`). Same send-and-await-completion pattern.

### Duplicated client-side passkey helpers

Some client-side passkey helper code is **intentionally duplicated** between the two scripts described above:

- `src/Blazor.Server.Backend/wwwroot/js/passkey-submit.js` — the template-aligned `<passkey-submit>` custom element loaded on the **login page**.
- `src/Blazor.Shared/Profile/ControlPanels/Passkeys/Components/AddPasskeyControlPanel.razor.js` — the profile "Add passkey" control panel module, which runs **inside the Blazor app**.

They run in separate JS contexts and cannot share a module without changing how the template-aligned login script is loaded. When fixing the duplicated helper code, apply the change to both scripts.

## Links

- [Enable WebAuth API passkeys](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/passkeys/?view=aspnetcore-10.0)
- [Passkeys in blazor](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/passkeys/blazor?view=aspnetcore-10.0&tabs=visual-studio&pivots=existing-app)
- [Changes in ef core 9](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-9.0/breaking-changes#pending-model-changes)
