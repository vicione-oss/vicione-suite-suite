# HostManagement capabilities

HostManagement 2.0 only allows the settings and actions that are enabled in `/etc/hostmanagement/SupportedCapabilities.conf`.
Without the file, all of them are disabled.

## Setting up a device

Edge devices come with the file, normally with everything enabled.
On other devices, create a template and enable the capabilities the device needs:

```shell
hostmanagement --create-supported-capabilities-template
```

Then uncomment the entries in `/etc/hostmanagement/SupportedCapabilities.conf` and set them to `Enabled`.

## What the Suite checks

The Suite still offers every setting and action; greying out disabled ones is #2932.
It reads the capabilities when an action starts and refuses the action before it changes anything.

| Action | Capability | When disabled |
|---|---|---|
| Restart System | `RestartSystem` | Error banner, nothing is sent |
| Shut down system (the button is currently hidden) | `ShutdownSystem` | Error banner, nothing is sent |
| Restart Application, Restart all instances | `RestartService` | Error banner, the Suite keeps running |
| Flash device (system update) | `UpdateSystem` | Save error, users stay logged in |
| Restore a backup that needs a Suite restart | `RestartService` | Error banner, nothing is restored |
| Restore a backup that changes a disabled setting | The setting, for example `Hostname` | Error banner naming the settings, nothing is restored |
| Failsafe page and downgrade page actions | `RestartService` | The action runs, then the page asks for a device restart |

"Restart all instances" checks only the host the user is on, see #2927.

When HostManagement cannot report its capabilities, the Suite skips its check and HostManagement decides on its own.

## What HostManagement checks alone

The network settings, DHCP lease renewal, device reset, the onboarding wizard and module service commands do not check the capabilities.
A module service restart uses the `RestartService` capability, the same one that restarts the Suite.
HostManagement rejects a disabled change or action with its own English message.

## Testing without HostManagement

The mock pipe client reads capabilities from a JSON file in the HostManagement `ApplyCapabilities` format.
Capabilities missing from the file stay enabled.

```json
{
  "Topics": { "RestartSystem": "Disabled", "RestartService": "Disabled", "UpdateSystem": "Disabled" },
  "Settings": { "DNS": { "Hostname": { "Capability": "Disabled" } } }
}
```

Start the Suite with the mock client enabled, as in `appsettings.Development.json`, and pass the file:

```shell
dotnet run --project src/Core.OS -- --HostManagement:MockClient:SupportedCapabilitiesJsonFile=<path to the file>
```
