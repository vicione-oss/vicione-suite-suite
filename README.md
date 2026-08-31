# ViciOne Suite
Welcome to the readme of ViciOne Suite. Questions and suggestions for improvement are warmly appreciated!

## Contents
* [Changelog](#changelog)
* [Prerequisites](#prerequisites)
    * [Software](#software)
    * [JFrog credentials](#package-api-credentials)
    * [npm packages](#npm-packages)
    * [Local development experience](#local-development-experience)
* [Getting Started](#getting-started)
* [ViciOne Suite Accounts](#vicione-suite-accounts)
* [Configuration](#configuration)
    * [MessageBus](#messagebus)
    * [MQTT](#mqtt)
        * [Predefine MQTT-Websocket-Client connection](#predefine-mqtt-websocket-client-connection)
        * [Connection details for Backend-Services](#connection-details-for-backend-services)
    * [Modules](./docs/modules.md)
    * [OpenTelemetry](#opentelemetry)
    * [OpenID Connect](#openid-connect)
        * [Using OIDC in development](#using-oidc-in-development)
* [Deployments](#deployments)
    * [Test Systems](#test-systems)
    * [Tools](#tools)
        * [Postgres](#postgres)
        * [RabbitMQ](#rabbitmq)
        * [ttyd](#ttyd)
        * [Access to DigitalOcean Review Deployments](#access-to-digitalocean-review-deployments)
* [Further information](#further-information)
    * [Database migration](#database-migration)

## Changelog
The Changelog is created according to https://keepachangelog.com/en/1.0.0/.

## Prerequisites
### Software
Prerequisites for development are:
- [Visual Studio](https://visualstudio.microsoft.com/vs/) - Version 17.2 or newer incl. Workload "ASP .NET and Webdevelopment"
- (optional) [nodeJS](https://nodejs.org/en/) Version 18.5 or newer

### Package API Credentials
To enable your development system to access these packages, you need to configure credentials so that _Core.OS_ can authenticate against the [JFrog Software Supply Chain Platform](https://system.update.ifm) API.

Modules can directly retrieve JFrog artifacts by using the `IArtifactQueryApi` interface, which is part of the `ViciOne.Suite.Sdk` package.

Currently, you must manually provide the credentials in one of the following ways:

- appsettings.json
- Environment variables
- (Recommended) Using .NET UserSecrets

Please contact the infrastructure team to obtain the required password.

Once you received the password, you can continue to add the credentials by e.g. UserSecrets:
1. Open a powershell and move into the `.../suite/src/Core.OS` directory
1. Execute the following commands<br>
```ps
dotnet user-secrets set "ArtifactRepository:Sources:0:UserName" "vicione-suite-readonly"
dotnet user-secrets set "ArtifactRepository:Sources:0:Password" "<password>"
```

You can add multiple sources for the repository e.g. to have access to ci-versions by adding a new source like here:
```ps
dotnet user-secrets set "ArtifactRepository:Sources:1:Endpoint" "https://system.update.ifm/artifactory/vicione-suite-dev"
dotnet user-secrets set "ArtifactRepository:Sources:1:UserName" "<username>"
dotnet user-secrets set "ArtifactRepository:Sources:1:Password" "<password>"
```

### npm packages
All npm packages need to be installed before starting the application.

To install the npm packages you have to:
1. Open a PowerShell and move into the `.../suite` directory (or run it from there)
1. Run the command `npm ci`
1. Run the command `npm run build`

The installation should now run automatically.

> Note: After executing commands such as git clean, this step likely needs to be repeated.

### Local development experience

Some aspects of the build process differs when running a build in a local developer environment compared to when it would run in CI pipeline.

We provide support for environment variables to customize the local development experience.

Environment Variable | Description | Default Value | Value in CI | Example usage in PowerShell
-|-|-|-|-
`TREAT_WARNINGS_AS_ERRORS` | When set to `true`, all compiler warnings are treated as errors. | `false` | `true` | `[Environment]::SetEnvironmentVariable("TREAT_WARNINGS_AS_ERRORS", "true", "User")`

### Dotnet Build

Now with all prerequisites fulfilled, build the **entire solution** using your IDE or by executing `dotnet build` in the `.../suite`-directory.

## Getting started
After cloning the ViciOne Suite repo and fulfilling the above-mentioned prerequisites, you are now ready to launch the application.

To do so:
1. Open the [solution](./vicione-suite.slnx) with Visual Studio/Rider/VS Code
1. Make sure the `startup-project` is set to **Core.OS**
1. Choose your preferred `instance mode`:
   1. **Standalone-Ui:** will start a standalone instance (**Recommended**)
   1. **Master-Ui:** will start a master instance
   1. **Slave1-Ui:** will start a slave instance
   1. **Slave2-Ui:** will start a slave instance
1. Start debugging (The first start might take a minute)
1. Open a browser and go to the URL https://localhost:XXXX*
1. Login using one of the below-mentioned [ViciOne Suite Accounts](#vicione-suite-accounts)

>\*Note: The port depends on chosen instance mode. For example, standalone listens on Port 5001. If you are unsure, check the console for an information like *"Now listening on: https://localhost:XXXX"*

>\*\*Note: To successfully start a master/slave setup, make sure to have the prerequisites installed and running (best to use `tests/compose.master-slave.yaml` to start Postgres, RabbitMQ and the Aspire Dashboard)

It is possible to start multiple instances using the launch profiles Master-Ui & Slave(1|2)-Ui.

## ViciOne Suite Accounts

| User     | Password       | Role          |
|----------|----------------|---------------|
| `Bob`    | `Up2noGood!!!` | User          |
| `Alice`  | `Up2noGood!!!` | User          |
| `Admin`  | `Up2noGood!!!` | Administrator |
| `Eddy`   | `Up2noGood!!!` | Administrator |

## Configuration
All settings of the application can be adjusted in the `/src/Core.OS/appsettings.[ENVIRONMENT].json`-file.
For easy development, by default all necessary parts of the suite run in memory. Further information is found below. 


### MessageBus
The MessageBus utilizes RabbitMQ for communication between Suite-Instances. During development, communication is performed *in-memory* by setting `MessageBus:UseInMemoryBus:true` in the `appsettings.json`-file. 

```json
"MessageBus": {
    "UseInMemoryBus" : false,
    "CleanVirtualHost": true,
    "QueueLifetimeInDays": 1,
    "MessageLifetimeInDays": 1,
    "AutoDeleteQueues": true,
    "Connection": {
        "Host": "localhost",
        "Port": "5672",
        "ManagementPort": "15672",
        "User": "test",
        "Pass": "test",
        "UseSsl": false
    }
}    
```

### MQTT

#### Predefine MQTT-Websocket-Client connection
You can predefine a connection for an MQTT-Websocket-Client using the `MqttClient.WebSocketClient`-Key. It will then be available in the Suite through Sdk.Connections. 

```json
"MqttClient": {
    "WebSocketClient": {
        "Endpoint": "wss://localhost/mqtt",
        "Port": 5001,
        "UserName": "user",
        "Password": "a-word-to-pass",
        "TopicFilter": "data"
    }        
}
```

### OpenTelemetry

For detailed information about OpenTelemetry configuration, please refer to the [OpenTelemetry Documentation](https://opentelemetry.io/docs/specs/otel/configuration/sdk-environment-variables/).

Sample configuration in `appsettings.json`:
```json
{
    "OTEL_EXPORTER_OTLP_ENDPOINT": "http://localhost:4317",
    "OTEL_ADDITIONAL_METERS": [
        "Meter.Sample1",
        "Meter.Sample2"
    ],
    "OTEL_ADDITIONAL_SOURCES": [
        "Source.Sample1",
        "Source.Sample2"
  ]
}
```

When using the runtime profiles from `launchSettings.json`, no additional configuration for OpenTelemetry is required.
Also, when starting, OpenTelemetry is set up to trace `MassTransit`, so adding it to the `OTEL_ADDITIONAL_SOURCES` list is not required.

For log export via OTLP and the specialities relevant to edge devices, see [OpenTelemetry](./docs/opentelemetry.md).

#### Connection details for Backend-Services
>:warning: Only use in development environment

Backend services can be provided with connection details using the `MqttClient.ServiceClient`-Key. This should only be used for development purposes. In production environments it is required to use `ConnectionManagement` to configure connections.

```json
"MqttClient": {
    "ServiceClient": {
        "Endpoint": "localhost",
        "Port": 1883,
        "UserName": "user",
        "Password": "a-word-to-pass"
    }
}
```

## UI Host
The UI Host is responsible for serving the Blazor application and static files. By default the UI Host is enabled and set to `ViciOne.Suite.Blazor.Server`. To change the UI Host, modify the `ModuleLoader:UiHost`-setting in the `appsettings.json`-file. If you remove the property or set `null` the suite will operate in headless mode, without providing any UI or client modules.

For development, UI host is configured to run on `https://localhost:5001`. You can modify the port and other settings in the `appsettings.json` file under the `Kestrel` section. 

### OpenID Connect

Using OIDC requires a provider to be configured. 
The provider can be configured with the following settings:

```json
"ExternalIdProviders": {
  "Providers": [
    {
      "Name": "GitLab",
      "Authority": "https://gitlab.com",
      "ClientId": "client id",
      "ClientSecret": "secret"
    }
  ]
}
```

> Currently, only **one provider** is supported, even though it is possible to configure multiple providers.

#### Using OIDC in development

For development purposes go to https://gitlab.com/-/user_settings/applications and register an application there.
Required are the grants:

- `openid`
- `email`
- `profile`

## Modules

Core.OS is searching for modules within the paths set in [appsettings.json](src/Core.OS/appsettings.json) under the `ModuleLoader`-section. All paths specified in `ModuleLoader:ModulesPath` will be searched for installed modules. Installed modules are defined in `modules.json` manifest that can be pre-seeded, otherwise it will get created after startup. For detailed information see [Suite-Modules](./docs/modules.md)

## Deployments
### Test Systems
There is a number of test systems that can be used. To do so, all deployments and feature branches can be deployed using their corresponding CI-Jobs. 

With each deployment on the machine, the content within the `./AppData`-directory is deleted and initialized with an new instance. If this behavior is __unwanted__, set the environment-variable `PRESERVE_DATA=true`. This will preserve the `./AppData`-directory. Also now any ongoing processes of the EngineManagement will __not__ be terminated. 

The following fixed deployments exist:

| Name                     | IP                | Instance     | Environment       |
|--------------------------|-------------------|--------------|-------------------|
| cloud-master             | `159.89.13.71`    | Master       | `master`          |
| cloud-presentation       | `167.71.50.41`    | Standalone** | `standalone`      |
| internal-slave01         | `10.45.24.192`    | Slave*       | `slave`           |
| internal-ec20-4-server-1 | `10.45.24.156`    | Standalone** | `standalone`      |
| internal-ec20-4-server-2 | `10.45.24.157`    | Standalone** | `standalone`      |
| internal-ec20-4-server-3 | `10.45.24.158`    | Standalone** | `standalone`      |
| internal-rpi5-server-1   | `10.45.24.201`    | Standalone** | `standalone`      |
| minimal                  | `10.45.24.155`    | Standalone** | `minimal`         |

>*Note: The `slave`-instances are **automatically** connected to the `master`-instance. 

>**Note: All `standalone`-instances use SQLite instead of Postgres. Therefore, they can be started without any dependencies to other systems.

### Tools
#### pgAdmin

| Property    | Value            |
|-------------|------------------|
| Address     | `<IP>:8080`      |
| User        | `admin@admin.de` |
| Password    | `admin`          |

#### postgres
| Property    | Value       |   
|-------------|-------------|
| Address     | `<IP>:5432` |
| User        | `postgres`  |
| Password    | `postgres`  |   

#### RabbitMQ
| Property    | Value       |
|-------------|-------------|
| Address     | `<IP>:8081` |
| User        | `rabbit`    |
| Password    | `rabbit`    |

#### ttyd
| Property            | Value        |
|---------------------|--------------|
| Address             | `<IP>:3000`  |
| Basic Auth User     | `moneo`      | 
| Basic Auth Password | `Up2noGood!` |
| User                | `debug`      |
| Password            | `debug`      |

To transfer files via the ttyd web interface you can use the zmodem protocol:  
- `sz <file>` initiate file transfer from the server to your local computer
- `rz` initiates a files transfer from your local computer to the server

#### Access to DigitalOcean Review Deployments
| Property     | Value     |
|--------------|-----------|
| Address      | `<IP>:22` |
| SSH-User     | `debug`   |
| SSH-Password | `debug`   |

To access the DigitalOcean deployments, you need to download the SSH-Keys (as an artifact in the Deploy-CI-Job or attached to the merge request). After that, you can establish a connection via WSL using the following command:

```bash
ssh debug@<IP> -i <path/to/ssh/key>
```

To navigate on the machine use the `debug`-User and password-less `sudo`.

To connect a review-instance (on DigitalOcean) to the `master`-instance, the environment configuration needs to be adjusted. This can be done by modifying the settings in the [appsettings.Review.json](src/Core.OS/appsettings.Review.json) file.

Furthermore, while connected as the `debug`-user, you can use the following commands:
- To __terminate__ the application use `sudo kill-suite`
- To __restart__ the application use `sudo restart-suite`
- To __delete__ content from the `./AppData`-directory use `sudo delete-appdata`
- To __stop docker containers__ use `sudo kill-container`. Afterwards, during the **next** deployment the pipeline will create new containers. Until then, they will not be available.

## Further information
### Database migration

To create an database migration for Core.OS or backend modules, it is recommended to use `Package Manager Console` of Visual Studio.

The following example creates migrations for a newly introduced entity `FooBar` in `ApplicationDbContext`:

``` powershell
Add-Migration Application_FooBar -OutputDir Migrations\ApplicationDbContext\Sqlite -Context ApplicationDbContextSqlite -StartupProject Core.OS

Add-Migration Application_FooBar -OutputDir Migrations\ApplicationDbContext\Postgres -Context ApplicationDbContextPostgres -StartupProject Core.OS
```

Alternatively using PowerShell from the `.../suite/src/Core.OS`-directory:
``` powershell
dotnet ef migrations add Application_FooBar -o Migrations\ApplicationDbContext\Sqlite -c ApplicationDbContextSqlite -s Core.OS.csproj

dotnet ef migrations add Application_FooBar -o Migrations\ApplicationDbContext\Postgres -c ApplicationDbContextPostgres -s Core.OS.csproj
```

Upon start, the application automatically creates required databases and executes pending migrations.

### Adding additional instance information

#### 1. The data flow, at a glance

There are **three distinct places** instance data lives, and **five mapping methods** in
`InstanceInformationMapper` that move data between them:

| Location | Type | Written by |
|---|---|---|
| Startup config | `InstanceOptions` | appsettings / deploy.sh |
| Wire message | `RegisterInstance` (command) | `ApplicationWorker`, `DbChangeSetConsumer`, `SyncRoutingSlipFaultedConsumer` |
| DB row (master) | `InstanceInformation` (EF entity) | `RegisterInstanceConsumer` |
| In-memory local cache | `InstanceInformation` (via `ILocalInstanceInformationProvider`) | `RegisterInstanceConsumer.UpdateInstanceProviders`, `ApplicationWorker.InitializeLocalInstanceInformation` |

#### 2. Checklist for a new field

1. **Add the property to `InstanceInformation`** (`Core.Shared.Instance.Contracts`) and,
   if other modules need to read it off `ILocalInstanceInformationProvider.Local`, add a
   matching get-only property to `IInstanceInformation` (`Sdk.Instance`).

2. **Persistence** — Add the EF Core
   mapping/configuration and a migration.

3. **Add it to the `RegisterInstance` command** (`Core.OS.Instance.Commands`) if the value
   needs to travel from an instance to the master over the bus.

4. **Update `InstanceInformationMapper`** — there are five methods, and a new field
   typically needs to be threaded through most of them:

   - **`ToInstanceInformation(this InstanceOptions, Guid)`**
     Only relevant if the field can be *seeded* from startup config on first boot. Follow
     the existing conditional pattern (`if (!string.IsNullOrEmpty(...)) info.X = ...`) —
     don't overwrite a good default with an empty config value unless it is required to change when the config is updated.

   - **`ToRegisterInstanceCommand(this IInstanceInformation, ...)`**

   - **`ToInstanceInformation(this RegisterInstance, DateTimeOffset registrationTime)`**
     Master-side/Standalone: builds a brand-new DB row for a never-before-seen instance. Map the
     field from the command.

   - **`ApplyTo(this RegisterInstance, InstanceInformation existing, DateTimeOffset registrationTime)`**
     Master-side/Standalone: updates an *existing* DB row on every re-registration. Map the field
     here too — but decide deliberately whether it should be unconditionally overwritten
     (like `Type`, `Version`, `InstalledModules`) or only overwritten when present (like
     `FormattedName`, which uses `if (command.FormattedName is not null)`).
     `FirstTimeRegistered` is intentionally **not** touched here — it must survive
     across re-registrations. Follow that pattern for any other "set-once" field.

   - **`ApplyTo(this IInstanceInformation source, InstanceInformation target)`**
     Used by `LocalInstanceInformationProvider.UpdateLocal` to refresh the in-memory
     cache other modules read from (e.g. on `RegisterInstanceConsumer` completion, or at
     startup before the first registration completes). Map the field here as well.
     Note `InRecoveryMode` is deliberately **excluded** from this method and set
     separately by the caller — if the new field is similarly "local-runtime-only" and
     must never be clobbered by a value copied from elsewhere, follow that pattern
     instead of adding it to `ApplyTo`.

5. **Decide the field's overwrite semantics up front** — three existing patterns to
   choose from:
   - *Always overwrite* (`Type`, `Version`, `InstalledModules`, `SdkVersion`, ...).
   - *Overwrite only if the incoming value is present* (`FormattedName`, `NamePreload` →
     `Name`, `DescriptionPreload` → `Description`) — protects a previously-set value
     from being blanked out by a message that didn't carry it.
   - *Set once, never touched by later registrations* (`FirstTimeRegistered`).
   - *Local-only, set outside the mapper* (`InRecoveryMode`).

6. **If the field affects sync/replication decisions** (the way `LastAppliedSequences`
   drives `RegisterInstanceConsumer.DetectSequenceMismatch`), also update:
   - `RegisterInstanceConsumer.HandleSlaveInstanceSynchronization` / `DetectSequenceMismatch`
   - Any full-sync trigger logic in `DbChangeSetConsumer`

#### 3. Common mistake to avoid

Adding a field to only `InstanceInformation` and `RegisterInstance` but forgetting one of
the two `ApplyTo` overloads is the most common way a field silently stops updating —
either the master DB row keeps a stale value on re-registration, or the local in-memory
cache (`ILocalInstanceInformationProvider.Local`) diverges from what's on the master.

### Update .NET-Framework

When upgrading .NET to a new major version, make sure to update the list of [Suite-Assemblies](./src/Sdk.Deployment/Scripts/suite-libraries.txt) using [Dependency-Tools](./tools/Suite.Deps/README.md).
