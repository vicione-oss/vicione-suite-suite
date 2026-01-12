# ViciOne Suite
Welcome to the readme of ViciOne Suite. Questions and suggestions for improvement are warmly appreciated!

## Contents
* [Changelog](#changelog)
* [Prerequisites](#prerequisites)
    * [Software](#software)
    * [Certificates](#certificates)
        * [Windows](#windows)
        * [Linux](#linux)
    * [JFrog credentials](#package-api-credentials)
    * [npm packages](#npm-packages)
* [Getting Started](#getting-started)
* [ViciOne Suite Accounts](#vicione-suite-accounts)
* [Configuration](#configuration)
    * [MessageBus](#messagebus)
    * [MQTT](#mqtt)
        * [Use internal broker](#use-internal-broker)
        * [Predefine MQTT-Websocket-Client connection](#predefine-mqtt-websocket-client-connection)
        * [Connection details for Backend-Services](#connection-details-for-backend-services)
    * [Modules](./docs/modules.md)
    * [OpenTelemetry](#opentelemetry)
* [Deployments](#deployments)
    * [Test Systems](#test-systems)
    * [Tools](#tools)
        * [Postgres](#postgres)
        * [RabbitMQ](#rabbitmq)
        * [ttys](#ttys)
        * [Access to DigitalOcean Review Deployments](#access-to-digitalocean-review-deployments)
* [Further information](#further-information)
    * [Database migration](#database-migration)
    * [Blazor Debuggig](#blazor-debugging)
    * [Update ViciOne.Ui.Shared.DX | .NET-Framework](#update-vicioneuishareddx--net-framework)

## Changelog
The Changelog is created according to https://keepachangelog.com/en/1.0.0/.

## Prerequisites
### Software
Prerequisites for development are:
- [Visual Studio](https://visualstudio.microsoft.com/vs/) - Version 17.2 or newer incl. Workload "ASP .NET and Webdevelopment"
- (optional) [nodeJS](https://nodejs.org/en/) Version 18.5 or newer

### Certificates
The repository's root-directory now contains the new Root-CA for our deployments. After installing it locally, all visited deployments will be green and marked as safe.

#### Windows
1. Double-Click the Certificate
1. Click `Install Certificate...`
1. Choose a Store Location. `Current user` should be sufficient, but `Local Machine` also works
1. Choose `Place all certificates in the following store`, click `Browse` and set location to `Trusted Root Certification Authorities`
1. Click through the remaining steps of the installation wizard

#### Linux
1. Copy Certificate **and** Key to `/usr/local/share/ca-certificates`
1. Execute `update-ca-certificates` with **elevated privileges** (`sudo` or as admin)

### Package API Credentials
To enable your development system to access these packages, you need to configure credentials so that _Core.OS_ can authenticate against the [JFrog Software Supply Chain Platform](https://system.update.ifm) API.

Modules can directly retrieve JFrog artifacts by using the `IArtifactQueryApi` interface, which is part of the `ViciOne.Suite.Sdk` package.

Currently, you must manually provide the credentials in one of the following ways:

- appsettings.json
- Environment variables
- (Recommended) Using .NET UserSecrets

Please contact the infrastructure team to obtain the required password.

Once you received the password, you can continue to add the credentials by e.g. UserSecrets:
1. Open a powershell and move into the `.../vo-suite/src/Core.OS` directory
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
1. Open a powershell and move into the `.../vo-suite` directory (or run it from there)
1. Run the command `npm install`
1. Run the command `npm run build`

The installation should now run automatically.

> Note: After executing commands such as git clean, this step likely needs to be repeated.

## Getting started
After cloning the ViciOne Suite repo and fulfilling the above mentioned prerequisites, you are now ready to launch the application. 

To do so:
1. Open the [solution](/vicione-suite.sln) with Visual Studio
1. Set the `startup-project` to **Core.OS** 
1. Choose your preferred `Emulator` (**Recommended** Backend-Server)
1. Start debugging (The first start might take a minute)
1. Open a browser and go to the URL https://localhost:XXXX*
1. Login using one of the below mentioned [ViciOne Suite Accounts](#vicione-suite-accounts)

>\*Note: The port depends on chosen Emulator. For example, Blazor Server listens on Port 5001. If you are unsure, check the console for an information like *"Now listening on: https://localhost:XXXX"*


## ViciOne Suite Accounts

| User     | Password       | Note                      |
|----------|----------------|---------------------------|
| `Bob`    | `Up2noGood!!!` | User                      |
| `Alice`  | `Up2noGood!!!` | User                      |
| `Admin`  | `Up2noGood!!!` | Administrator             |
| `Eddy`   | `Up2noGood!!!` | Administrator + SA claims |

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

Upon start, the application automatically creates required databases and executes pending migrations.

### Update ViciOne.Ui.Shared.Dx | .NET-Framework

When updating our Blazor-library, check whether the referenced DevExpress.Blazor-Nugets have undergone a Minor-Version update or greater (e.g. 22.1.x to 22.2.x). If so, make sure to update the list of [Suite-Assemblies](./src/Sdk.Deployment/Scripts/suite-libraries.txt) using [Dependency-Tools](./tools/Suite.Deps/README.md).
