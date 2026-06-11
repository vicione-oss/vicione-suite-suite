# Blazor.Server.Backend

Blazor Server UI host — provides rendering engine, authentication UI, module UI loading, and browser interop.

- Implements `IUiHostModule` — dynamically loads client module UIs
- **Must NOT contain business logic** — delegation layer only; logic belongs in Core.OS or module backends
- Controllers handle cookie-based auth/localization updates
- DevExpress Blazor components are being replaced by `blazor-components` repo — prefer custom components for new UI
- Razor CSS isolation uses `suite-server` scope

