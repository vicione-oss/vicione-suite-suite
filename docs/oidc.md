# ViciOne OpenIdConnect

## Configuration

External OpenID Connect providers are configured through the `ExternalIdProviders` configuration section.
Each entry under `Providers` describes one provider users can sign in with:

| Setting        | Required | Description                                                                                                                                                    |
|----------------|----------|----------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Name`         | yes      | Display name of the provider, e.g. shown on the login page.                                                                                                    |
| `Authority`    | yes      | Base URL of the provider's OIDC issuer, e.g. `https://gitlab.com`. Its discovery document must be reachable at `<Authority>/.well-known/openid-configuration`. |
| `ClientId`     | yes      | The application/client id issued by the provider.                                                                                                              |
| `ClientSecret` | no       | The client secret issued by the provider. Required for confidential clients.                                                                                   |

Example as JSON (`appsettings`):

```json
{
  "ExternalIdProviders": {
    "Providers": [
      {
        "Name": "GitLab",
        "Authority": "https://gitlab.com",
        "ClientId": "<client-id>",
        "ClientSecret": "<client-secret>"
      }
    ]
  }
}
```

The same configuration as environment variables (for example in `/etc/vicione-suite/conf.d/`):

```ini
ExternalIdProviders__Providers__0__Name=GitLab
ExternalIdProviders__Providers__0__Authority=https://gitlab.com
ExternalIdProviders__Providers__0__ClientId=<client-id>
ExternalIdProviders__Providers__0__ClientSecret=<client-secret>
```

### Redirect URI

The suite uses the ASP.NET Core default callback path `/signin-oidc`. Register the following redirect URI at the
provider, using the exact URL under which the suite is reached:

```
https://<host>/signin-oidc
```

The provider matches this value exactly — scheme, host, port and path must all match the URL the suite generates.
Most providers (including GitLab) require `https` for any non-loopback host, so the suite must be served over `https`.

### Running behind a reverse proxy

A common deployment terminates TLS at a reverse proxy (for example the bundled nginx) and forwards the request to the
application over plain `http`. The application reconstructs the original scheme, host and client address from the
proxy's forwarding headers (`X-Forwarded-Proto`, `X-Forwarded-Host`, `X-Forwarded-For`) — but by default it only
believes those headers when the request was delivered by a proxy on **loopback**, because any client can send them.

- **Proxy on the same host** (the bundled nginx, `proxy_pass http://127.0.0.1:5000`): works out of the box, no
  configuration needed.
- **Proxy on a different host**: the loopback safelist rejects it, the application keeps seeing `http`, builds an
  `http://<host>/signin-oidc` redirect URI, and the provider rejects it (see [Troubleshooting](#troubleshooting)).
  In this setup add the proxy's address (or network, in CIDR notation) to the safelist:

  ```ini
  Instance__TrustedProxies__0=10.0.0.5
  Instance__TrustedProxies__1=192.168.178.0/24   # further entries as needed
  ```

The proxy must actually set the headers; for nginx the relevant directives are:

```nginx
proxy_set_header Host              $host;
proxy_set_header X-Forwarded-Proto $scheme;
proxy_set_header X-Forwarded-Host  $host;   # set explicitly so clients cannot inject it
```

> **Security:** `TrustedProxies` *extends* the safelist — forwarding headers from senders not on the list (and not on
> loopback) are still ignored, so a client reaching Kestrel directly cannot spoof scheme, host or originating IP.
> List only addresses that actually are trusted proxies.

> **Deprecated:** the earlier boolean `Instance__UseHeaderForwarding=true` is still honoured but deprecated and will be
> removed in a future release. It removes the source safelist entirely, so the application trusts forwarding headers
> from *any* sender — safe only when the application is reachable exclusively through the proxy. Migrate by replacing
> the flag with `Instance__TrustedProxies__*` entries naming your proxy; when both are set, `TrustedProxies` wins and
> the safelist stays active.

## Differences from Microsoft templates

### Handlers

As we do not have a user profile page that enables linking to the respective pages for password, 2FA, ...
we handle the backend part not in razor files, but in MinimalAPI handlers.

### Username for login

Other than the default Microsoft template, which uses the email as the username, we use the username itself.
While we can get the email address of the user from the claims from OIDC, we rely on `preferred_username` as the username.

`preferred_username` is _not_ constant and even discouraged by Microsoft for login:
https://learn.microsoft.com/en-us/entra/identity-platform/id-token-claims-reference#use-claims-to-reliably-identify-a-user

#### Implications

Using `preferred_username` to initialize a new user's username means that when a user changes their `preferred_username` at the external provider it will not be reflected in ViciOne. 
ASP.NET Core Identity makes sure that the [username is unique](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0#default-model-configuration),
so it solely has implications on the user experience.

## Troubleshooting

### The external provider rejects the redirect URI

**Symptom:** After being redirected to the external provider, sign-in fails with an error
such as *"The redirect URI included is not valid."* (GitLab/Doorkeeper) or a generic `redirect_uri` mismatch.

**Cause:** The `redirect_uri` the suite sent does not exactly match the one registered at the provider. Behind a
TLS-terminating reverse proxy the most common cause is a scheme mismatch: the registered URI is
`https://<host>/signin-oidc`, but the suite generated `http://<host>/signin-oidc`. This happens because the proxy
terminates TLS and forwards the request to the application as plain `http`; if its forwarding headers are missing or
not trusted (proxy neither on loopback nor listed in `TrustedProxies`), the application builds the redirect URI with
the `http` scheme.

**Diagnosis:** Open the authorization request URL the browser is redirected to and url-decode its `redirect_uri`
query parameter. If it begins with `http://` while the registered URI uses `https://`, this is the cause.

**Fix:**

1. Ensure the proxy forwards the scheme, e.g. nginx `proxy_set_header X-Forwarded-Proto $scheme;`.
2. If the proxy runs on a different host than the suite, add it to the safelist with
   `Instance__TrustedProxies__0=<proxy-ip-or-cidr>` (see
   [Running behind a reverse proxy](#running-behind-a-reverse-proxy)); a loopback proxy is trusted by default.
3. Ensure the request actually reaches the proxy over `https`.
4. Verify the registered redirect URI matches the suite's URL exactly (scheme, host, port and path).

