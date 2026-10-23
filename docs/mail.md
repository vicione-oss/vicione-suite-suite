# Mailing

The mailing subsystem allows the application to send emails (e.g., password resets, account verification)
via an SMTP server.

## Usage for Account Verification

Only if `RequireAccountVerificationToLogIn` is set to `true` in the `UserManagementOptions` section, and an SMTP server
is configured, in SMTP settings, accounts are required to verify their email address before they can log in.

## Configuration

To enable and configure the mailing subsystem, you need to adjust the following sections in your `appsettings.json` (or
`appsettings.Development.json`).

### 1. SMTP Settings (`SmtpMailOptions`, Section "Smtp")

Configure the connection to your SMTP provider.

* **ServerAddress**: The network DNS or IP address of the SMTP server.
* **ServerPort**: The port to connect to, usually 587. The server must support STARTTLS; port 465 (implicit TLS) is not supported.
* **UserName**: The name used to log in to the server, often but not always an email address. It never appears in
  the mails.
* **FromAddress**: The email address that will appear in the "From" field.
* **FromName** (optional): The display name in front of `FromAddress`, e.g. `ViciOne Suite`. Without it, mails show
  the bare address.
* **Password**: The password for the sender's account.

To check the values an instance uses, open the **SMTP** tab of the instance in Settings: Settings → Instance on a
standalone instance, Settings → Instances → the instance in a cluster. The tab is read-only and shows only whether a
password is set, never the password itself.

### 2. User Management Settings (`UserManagementOptions`, Section "UserManagement")

* **RequireAccountVerificationToLogIn**: If set to `true`, the system will send an account verification email upon
  registration, and the user will not be able to log in until they click the link in that email.
