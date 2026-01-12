# ViciOne Suite Edge-s

## Available Edge-s Installations

Several Edge-s systems are available in our network for testing purposes:

| IP             | Instance   |
| -------------- | ---------- |
| `10.45.24.220` | Standalone |
| `10.45.24.221` | Standalone |
| `10.45.24.222` | Standalone |
| `10.45.24.223` | Standalone |
| `10.45.24.224` | Standalone |
| `10.45.24.225` | Standalone |
| `10.45.24.226` | Standalone |
| `10.45.24.227` | Standalone |
| `10.45.24.228` | Standalone |

## SSH (WSL)

To log in to the system via SSH, use the *production* user account with the following command:

```bash
ssh <edge-s-ip> -l production
```

## First Login After Reset

The pre-installed user is **Administrator** with the password **Pa\$\$w0rd**. You will be required to change the password upon first login.

## Environment Variables

To check the currently used environment variables for the suite, run:

```bash
cat /etc/vicione-suite/conf.d/*
```

Later lines override earlier ones.

To add personal settings, create or edit a `.conf` file:

```bash
sudo nano /etc/vicione-suite/conf.d/50-my-settings.conf
```

To allow installation of modules on the edge-s and increase the log-level to `Information` you write this into `50-my-settings.conf`
```text
ModuleLoader__AllowInstallation='true'
Logging__LogLevel__Default='Information'
```

To add other artifact repository sources to access staging or development versions you need to add more environment variables to e.g. `50-my-settings.conf`. By default only the production repository is configured. Here is an example to add staging and development repositories:
```text
ArtifactRepository__Sources__1__Endpoint = "https://system.update.ifm/artifactory/vicione-suite-staging"
ArtifactRepository__Sources__1__UserName = "<username>"
ArtifactRepository__Sources__1__Password = "<password>"

ArtifactRepository__Sources__2__Endpoint = "https://system.update.ifm/artifactory/vicione-suite-dev"
ArtifactRepository__Sources__2__UserName = "<username>"
ArtifactRepository__Sources__2__Password = "<password>"
```

## Image Version
To check the currently installed version of the edge-s image, run:
```
sudo -u vicione-suite cat /etc/version
```

## Filesystem

Many paths used by the suite installation can be modified through environment variables. Default settings are:

```bash
Instance__BackupDirectory='/var/lib/vicione-suite/Backup/'
Instance__CacheDirectory='/var/lib/vicione-suite/Cache/'
Instance__HomeDirectory='/mnt/persistent/vicione-suite/AppData'
```

## Logs

All logs for the suite and engine hosts are written to the system journal and can be accessed with the `journalctl` command. For example, to follow the latest logs:

```bash
sudo journalctl -u vicione-suite -f
```

## Manual Suite Update

To manually update the suite:

1. Download the `.deb` package (e.g., from the pipeline step `Suite Debian packaging`), such as `vicione-suite_version_arm64.deb`.

2. Upload the package to the Edge-s system:

   ```bash
   scp /path/to/vicione-suite_version_arm64.deb production@<edge-s-ip>:/home/production
   ```

3. Log in to the Edge-s:

   ```bash
   ssh <edge-s-ip> -l production
   ```

4. Install the package:

   ```bash
   sudo apt install ./vicione-suite_version_arm64.deb
   ```

The suite will be (re)started automatically after the update and will be reachable shortly thereafter.
