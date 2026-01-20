# ViciOne Suite Deps

The tool analyzes the assembly dependencies of the Suite using the `SuiteDependencyContexts`. The tool provides different options:

1. `core` - Create a [list of all assemblies](https://gitlab.com/vicione-oss/vicione/suite/suite-sdk/-/blob/master/src/Sdk.Deployment/Scripts/suite-libraries.txt?ref_type=heads) provided by the Suite and optional by the referenced UiHost. These assemblies can be removed from module deployments safely to reduce their size significantly. Used by [assembly-cleanup.sh](https://gitlab.com/vicione-oss/vicione/suite/suite-sdk/-/blob/master/src/Sdk.Deployment/Scripts/cleanup-module.sh?ref_type=heads).
2. `modules` - Create a list of all module assemblies that are redundant when loaded by the suite. These assemblies can be removed from module deployments safely. 
3. `mapping` - Output the json structure of assembly version mappings used by the Suite with the provided modules. 

## Usage

`ViciOne.Suite.Deps.exe -h` shows the help for the provided operation modes.

`ViciOne.Suite.Deps.exe <mode> -h` shows the options for the specified operation mode

### Examples

The `core` option needs a path parameter `s` pointing to an existing Suite installation:
- `ViciOne.Suite.Deps.exe core -s "/path/to/published-suite/"`

The `modules` option needs a path parameter `s` pointing to an existing Suite installation. Additionally, it requires a list of module ids to be included in the analysis process:
- `ViciOne.Suite.Deps.exe modules -r -s "/path/to/published-suite/" -m ClusterManagement Burger JitChat Ping`

The `modules` option needs a path parameter `s` pointing to an existing Suite installation. Optionally you can pass a filepath to parameter `o` to write the output directly to a file instead of console:
- `ViciOne.Suite.Deps.exe mapping -r -s "/path/to/published-suite/"` 
