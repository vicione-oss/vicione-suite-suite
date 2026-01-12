# Suite Modules

## Contents
* [Installation](#installation)
  * [Settings UI](#settings-ui)
  * [Manual Installation](#manual-installation)
  * [Module Options](#module-options)
* [Debugging](#module-debugging)
* [UiHost](#uihost-modules)
* [Publish/Cleanup Modules](#publishcleanup-modules)

Modules are used to add features to _Core.OS_. To create a new module you can use this [Dotnet Template](https://gitlab.i40.ifm-datalink.net/acx/vo-suite/dotnet-templates). A module consists of different parts represented by different project types with following naming convention:

- `ModuleName.Backend`: Provide services, controllers, databases etc. and use suite infrastructure like the message bus directly.
- `ModuleName.Client`: Provide user interfaces like components and pages based on Blazor. 
- `ModuleName.Internal`: Share functionality and code between Backend and Client
- `ModuleName.Public`:  Provide features, contracts, etc. that can be used by other modules when the add a reference to it. This will create a dependency from one to another module. If ModuleB references ModuleA.Public, then ModuleB can only be used within Core.OS if ModuleA is installed.

The `ModuleName` is the output assembly name without the suffixes e.g. backend assembly is `ViciOne.Suite.Some.Module.Backend.dll`, then `ModuleName` is `ViciOne.Suite.Some.Module`.

Before a module gets loaded by _Core.OS_ its dependencies and referenced SDK version will be validated by analyzing the `Module.*.deps.json` file on startup to avoid assembly version mismatches.

## Installation
Changes to module configuration either un-/installing or modifying options will only be applied after restarting the system. If you install a module that requires additional parameters like accesskeys, username, paths it won't be loaded until at least the required parameters are set.
Missing module versions will be downloaded on demand by from http source. The source can be configured by using [ArtifactRepositoryOptions](../src/Core.Module/Options/ArtifactRepositoryOptions.cs) set by appsettings.json, environment etc. Actually this [Nexus](https://nexus.nsc-gmbh.de/#browse/browse:raw-fb-server:modules) is used by our deployments.

The installation of new modules and the modification of existing ones can be disabled by setting the `AllowInstallation` flag of the [ModuleLoaderOptions](../src/Core.Module/Options/ModuleLoaderOptions.cs) to `false`. The module manifest itself can still be modified manually if necessary.  Modules can be installed and configured in two different ways as described in the following sections.

### Settings UI
Start _Core.OS_ and use an admin account for authentication. Goto `Settings`->`System`->`Modules` to manage _Core.OS_ modules. There are two different areas available:

- `Installed Modules`: Lists already installed modules. Here you can edit module options, update modules and retrieve information about startup errors etc.
- `Browse Modules`: Browse through available modules for your _Core.OS_ version. Only modules with matching SDK versions will appear here. You can select multiple modules at once for installation. By default you will only find released module versions here but you can also include existing pre-release versions by enabling the 'Pre-Release' switch and `Refresh` action.

Any changes to installed or available modules need to be confirmed before they get applied. After restarting the system applied changes will be processed, downloading missing modules, using modified options etc. A persisted version of these options gets stored in `AppData` folder. The persisted options can still be overriden by environment variables or user secrets. 

### Manual Installation
In `AppData` directory you can find `modules.json` file that contains the installed module definitions. If you modify your module configuration using the UI this file will get modified. Of course you can edit it manually for development reasons. Add a module by adding a new entry to `modules.json` like:
```json
{
    "Name": "ModuleName",
    "Version": "0.2.0"
}
```

If this module depends on another module you need to extend the entry like here:
```json
{
    "Name": "ModuleName",
    "Version": "0.2.0",
    "DependingOn": [
        {
          "Name": "OtherModuleName",
          "Version": "0.24.0"
        }
      ]
}
```

Sometimes you would like to deploy a module pre-release on a test system. To achieve that you have to enter the pre-release version in the [modules.json](/src/Core.OS/modules.json) packages section. Use the `Version` property to override the package version.
```json
{
    "Name": "ModuleName",
    "Version": "ci-1298597"
}
```

### Module Options
A module might need options required to work properly. These can either be edited using the [Settings UI](#settings-ui) or manually by adding the needed options as environment variables, appsettings.conf or [user secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-9.0&tabs=windows). The key to set an option is defined by the module id without dots and the variable name.
If we have a module id like `ViciOne.Suite.ModuleA` and an option like `EnableFeature`, the key to set the value is `ViciOneSuiteModuleA:EnableFeature`. Dots are not allowed because they are not supported for linux environment variables.

## Module Debugging
For debugging purposes, `ModuleLoader:ModuleDebugPaths` can be used as a list of paths for the suite to search for backend/client modules. By doing so, the _Core.OS_ can be started directly from the module-project to debug local code. If a module is already installed but a debug location is configured for the same module it gets loaded from debug path. If there's a `module-metadata.json` in the module source path root it gets loaded as the module would really be installed.

```json
"ModuleLoader": {
    "UiHost": "UiHostModuleId"
    "ModulesPath": "path\to\publish\Modules",    
    "ModuleDebugPaths" : [
        "path\to\debug\Module1",
        "path\to\repo\src"
    ]
}
```

## UiHost Modules
_Core.OS_ itself does not supply a user interface, but by loading `ViciOne.Suite.Blazor.Server` as UIHost Blazor webui is supported. The corresponding module must be configured in the `ModuleLoader:UiHost` section. Currently, only one UiHost-Module can be activated. This is done by including the corresponding entry in `appsettings.json`. The following example configures _Core.OS_ to use `ViciOne.Suite.Blazor.Server` as UiHost:

```json
"ModuleLoader": {
    "UiHost": "ViciOne.Suite.Blazor.Server",
    "UiHostsPath" : "path\\to\\publish\\UiHosts"
}
```

While developing the Suite, set `UiHost:UseDebugRoot=true` to resolve local assets (CSS, JS, ...) using `staticwebassets.json`.
When loading an UiHost module in _Core.OS_, a FileProvider for their static.webassets is provided for all activated client modules. During `Debug` mode, all resource requests are resolved using the `static.webassets.json` in the respective output directory of the client module.

## Publish/Cleanup Modules
[Here](https://gitlab.i40.ifm-datalink.net/acx/vo-suite/vo-suite/-/tree/master/src/Sdk.Deployment/Scripts?ref_type=heads) are bash scripts to publish and cleanup a Suite-Module. For local development purposes you can also use [these](https://gitlab.i40.ifm-datalink.net/-/snippets/122) powershell scripts.

To execute the `publish-module.sh` script you can use: `./publish-module.sh ModuleName PathToSrcFolder linux-x64 PathToOutputFolder`

To execute the `cleanup.sh` script you can use: `./cleanup-module.sh PathToOutputFolder` - it will free the published module from assemblies provided by suite environment.
