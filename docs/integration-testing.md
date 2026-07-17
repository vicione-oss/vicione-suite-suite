# Testing Mail over SMTP

The mail integration tests live in `Core.OS.Tests.Mail.MailingTests` (trait `Category=Integration`)
and send real mail over SMTP using STARTTLS + authentication against a [mailpit](https://mailpit.axllent.org/)
instance.

The SMTP target is resolved from the environment, so the same tests run locally and in CI without code
changes:

| Variable                | Default                 | Meaning                          |
|-------------------------|-------------------------|----------------------------------|
| `MAILPIT_SMTP_HOST`     | `localhost`             | SMTP host the tests connect to   |
| `MAILPIT_SMTP_PORT`     | `1025`                  | SMTP port                        |
| `MAILPIT_SMTP_USERNAME` | `smtp-tester`           | SMTP auth user                   |
| `MAILPIT_SMTP_PASSWORD` | `MailpitTest123!`       | SMTP auth password               |
| `MAILPIT_API_URL`       | `http://localhost:8025` | mailpit HTTP API (delivery check) |

The success test does more than confirm the send did not throw: it tags the mail with a unique identifier,
then queries mailpit's API (via `MailpitClient`) to find that message by the identifier and asserts the
recipient, subject and sender match. New mail-related checks should reuse `MailpitClient` rather than
calling the API inline.

The credentials are supplied via the environment: the tests read `MAILPIT_SMTP_USERNAME` /
`MAILPIT_SMTP_PASSWORD`, and mailpit is configured with the same pair via its
[`MP_SMTP_AUTH`](https://mailpit.axllent.org/docs/configuration/smtp/#passwords-via-environment) variable.
STARTTLS uses mailpit's [`sans:` syntax](https://mailpit.axllent.org/docs/configuration/certificates/),
which generates a temporary self-signed certificate at startup, so no certificate has to be shipped
or maintained in the repository (the tests accept any server certificate).

## Running the tests locally

mailpit is managed by [mise](https://mise.jdx.dev/) (see `mise.toml`), so `mise install` provides the
`mailpit` binary. Start it (run from the repository root):

```bash
MP_SMTP_AUTH='smtp-tester:MailpitTest123!' mailpit \
  --smtp 0.0.0.0:1025 \
  --smtp-tls-cert  sans:localhost \
  --smtp-tls-key   sans:localhost \
  --smtp-require-starttls
```

Then run the integration tests (defaults already point at `localhost:1025`):

```bash
dotnet test tests/Core.OS.Tests/Core.OS.Tests.csproj --filter-query "/[Category=Integration]"
```

mailpit also offers a web UI at http://localhost:8025/ to inspect captured mail. Alternatively, run it via
Docker — see https://mailpit.axllent.org/docs/install/.

## Running in CI

The `Integration tests` job in `.gitlab-ci.yml` is a **manual** job — it does not run
automatically alongside the unit tests; trigger it on demand (the play button) on a merge request, the
default branch, or a web pipeline. Mailpit runs as a GitLab CI **service** (`axllent/mailpit`,
pinned to the same version mise provides), configured entirely through `MP_*` environment variables —
`MP_SMTP_AUTH` for credentials, `MP_SMTP_REQUIRE_STARTTLS`, and `MP_SMTP_TLS_CERT`/`MP_SMTP_TLS_KEY`
set to `sans:mailpit` so mailpit generates its own self-signed certificate. GitLab starts and awaits the
service, so the tests reach it by its alias hostname (`mailpit:1025`, API `http://mailpit:8025`) with no
start-up scripting in the job. The tests resolve these via the same environment variables as locally
(see the table above), so no code changes are needed between local and CI runs.

> The previous shared instance at `mailpit.infra.ifm-sw.net` has been **decommissioned** — it was only
> reachable inside the company network and became unavailable after the move to public GitLab.

## Pattern for other external-service integration tests

This sets the template for integration tests that need an external service (database, broker, ...):

1. Make the service available locally — e.g. add it to `mise.toml` so a developer can start it with
   no extra tooling.
2. Resolve its connection details via `Core.Tests.Tools.IntegrationServiceSettings` — an environment
   variable with a `localhost` default — instead of hard-coding a host.
3. In CI, prefer running it as a GitLab [`services:`](https://docs.gitlab.com/ci/services/) container
   (as the mail job does) so GitLab manages its lifecycle; point the tests at it by overriding that
   environment variable with the service alias. Starting it inline in the job is the fallback when no
   suitable image exists or the service needs files the stock image can't provide.

End-to-end UI tests (Playwright) live in their own document — see [e2e-testing.md](e2e-testing.md).


# Testing OIDC

To test OIDC features, you need the respective provider and configure it accordingly.
Luckily, this can easily be done using GitLab:

1. Go to https://gitlab.com/-/user_settings/applications
2. Select "Add new application"
3. Enter a name
4. As redirect URI enter `https://localhost:5001/signin-oidc` (if your local instance uses a different port than 5001, adjust accordingly)
5. You can decide whether you want a confidential client; both should work, for simplicity deselect it
6. You need to grant the following scopes:
   1. openid
   2. email
   3. profile
7. Select save
8. Store the application ID (and if you created a confidential client, the secret)
9. Add the client info to appsettings.Development.json:

```json
    "ExternalIdProviders": {
        "Providers": [
            {
                "Name": "GitLab",
                "Authority": "https://gitlab.com",
                "ClientId": "<client ID>",
                "ClientSecret": "<secret only if you used a confidential client, otherwise delete this entry>"
            }
        ]
    }
```

Now, when starting the Suite, it should offer the possibility to log in via OpenID.
