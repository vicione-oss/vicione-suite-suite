# Testing Mail over SMTP

## mailpit via docker

To locally test sending of mails via SMTP, start a local server replacement and alter the configuration in
`Core.OS.Tests.Mail.MailingTests` to point to localhost.

Start a local instance via command line:

```bash
docker run -d \
--restart unless-stopped \
--name=mailpit \
-v ../tests/Core.OS.Tests/Mail:/data \
-p 1025:1025 \
-p 8025:8025 \
-e MP_SMTP_AUTH_FILE=/data/mail.passwords \
-e MP_SMTP_AUTH_ALLOW_INSECURE=false \
-e MP_SMTP_TLS_CERT=/data/cert.pem \
-e MP_SMTP_TLS_KEY=/data/key.pem \
-e MP_SMTP_REQUIRE_STARTTLS=true \
axllent/mailpit
```

Mailpit now accepts SMTP connections on port 1025 and offers a web UI at http://localhost:8025/.

For other installation methods refer to https://mailpit.axllent.org/docs/install/

## mailpit integration testing instance

There is a testing instance that can be used for integration testing, running at:

* http://mailpit.infra.ifm-sw.net:8025 (Web UI)
* mailpit.infra.ifm-sw.net:1025 (SMTP)
