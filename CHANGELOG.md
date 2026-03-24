# Changelog

## 1.2.0 - Unreleased

### Added

- A button to copy system information for easy bug reporting was added to the `System & Notification` sidebar
- Support for external OIDC providers, allowing users to authenticate via third-party identity providers
- Configuration options for OIDC discovery endpoints, client IDs, and client secrets
- Load average into `ProcessComponent`

- `Microsoft.AspNetCore.Authentication.OpenIdConnect` package
- `JournalListView`, added download feature of Journal entries as text
- Added implementation of typed `ILocalHttpClient` to access local services with proper base address
- Added control panel `Sources` to manage artifact update sources, that provide suite, module updates
- Add indicator to system monitoring component

### Changed

- Change `ArtifactRepositorySourceOption` options, add property `TokenEndpoint`
- Replaced `DxMemo` with scrollable HTML element
- Replaced `DxPopup` with `ViciOne.Ui.Blazor.Components.Popup`
- `SystemDefaultControlPanelPage`, changed wording in description banner
- Redesign `ModuleManagementControlPanel` to grid layout with details page

### Removed

- Removed dependency to Material design icons
- `SettingsContainerContentHeader`, removed workaround to control size of "navigate back" icon, replaced by using larger icon and placement adjustments

### Fixed

- Tags in `ConnectionChanged` event after deleting a connection will now be properly filled

### Updated

- `AspNetCore.SassCompiler` packages, update to version `1.97.1`
- `AwesomeAssertions` packages, update to version `9.4.0`
- `Bunit` packages, update to version `2.6.2`
- `Npgsql.EntityFrameworkCore.PostgreSQL` package, update to version `10.0.2`
- `MassTransit` packages, update to version `8.5.8`
- `MailKit` package, update to version `4.15.1`
- `Microsoft` packages, update to version `10.0.5`
- `MQTTnet` packages, update to version `5.1.0.1559`
- `OpenTelemetry` packages, update to version `1.15.0`
- `Riok.Mapperly` packages, update to version `4.3.1`
- `Serilog.AspNetCore` packages, update to version `10.0.0`
- `Serilog.Sinks.Journal` package, update to `1.1.0`
- `Serilog.Sinks.SyslogMessages` packages, update to version `4.0.0`
- `ViciOne.CodeAnalysis` packages, update to version `1.4.0`
- `ViciOne.Journal` package, update to version `1.1.0`
- `ViciOne.Suite.Sdk` packages, update to version `2.0.0-ci2397913518`
- `ViciOne.Ui.Blazor.Components` package, update version to `5.6.0`
- `ViciOne.Ui.Design` package, update version to `2.0.3`
- `ViciOne.Ui.MonochromeIcons` packages, update version to `4.6.0`
- `ViciOne.Ui.Localization` package, update version to `3.2.0`
- `ViciOne.Ui.Shared.Dx` packages, update to version `0.19.1`
- `ViciOne.SystemMonitoring` packages, update to version `1.0.0`
- `xunit` packages, update to version `xunit.v3 - 3.2.2`
- `xunit.extensibility.core` packages, update to version `xunit.v3.extensibility.core - 3.2.2`

## 1.1.0 - 2025-12-18

### Added

- `ViciOne.Ui.Design` package as build-time dependency
- Add `UserTicketStore` to use reference cookies with fixed size to respect header limit
- Add `UserTicketCleanupService` to remove expired user tickets regularly
- Add role management allowing management of custom roles
- Registry services for control panels and control panel pages
- The new Administrator role is now created initially replacing existing default roles
- Registry services for navigation tiles
- Registry services for notification elements
- Disable login button backend if backend is unhealthy. Enable it if backend is healthy
- Respect new `DisableDefaultFeature` flag in `BackendModule`
- Support OpenTelemetry metrics and tracing
- Add support for multiple artifact repository sources
- Administrators can now set the password of other users in the User Management settings dialog
- Add system shutdown functionality
- Add `InstanceOptions.ServiceName` as configurable service name for HostManagement
- Add SMTP-based mailing subsystem (templates + MailKit)
- Add password reset flow (forgot password, reset password, reset confirmation)
- Add conditional email account verification and confirmation page for identity
- Add reboot system feature to system information area
- Add database context registration logic in the form of `ModuleDbContextRegistrar` from sdk to suite

### Changed

- Increase process overview access rights to system module full
- Replaced custom loading spinner implementations with `ContentLoadingIndication`
- Connections defined as `Managed` can no longer be deleted in the UI
- Changed `DateTime` to `DateTimeOffset`
- Reworked UI event forwarding process
- Show current time frame minutes in log center
- Count first log entry on initial run in monitoring
- Refactor database connection handling to use specific connection types for SQLite and PostgreSQL
- Support manual MAC address configuration in network settings
- Refactor instance and system control messaging (shutdown flows and instance administration), replacing legacy control commands/events
- Identity UI: add “Forgot your password?” link and reuse shared instance information partial
- NTP control panel: removed “adopt/copy system default servers” UI action
- Local requests will now be handled without MassTransit infrastructure when implementing `RequestConsumer<,>`. This means even large objects can be handled without concern for message size limits and without the overhead of serialization and deserialization.

### Updated

- `AspNetCore.SassCompiler` packages, update to version `1.94.2`
- `DevExpress.Blazor` packages, update to version `24.2.12`
- `HostManagement` packages, update to version `1.2.0`
- `MassTransit` packages, update to version `8.5.7`
- `Microsoft` packages, update to version `9.0.11`
- `Npgsql.EntityFrameworkCore.PostgreSQL` package, update to version `9.0.4`
- `Riok.Mapperly` packages, update to version `4.3.0`
- `System.IO.Abstractions` packages, update to version `22.1.0`
- `ViciOne.Suite.Sdk` packages, update to version `1.1.0`
- `ViciOne.Ui.Blazor.Components` package, update version to `4.3.0-ci1965647`
- `ViciOne.Ui.Design` package, update version to `1.1.1`
- `ViciOne.Ui.MonochromeIcons` packages, update version to `3.10.0`
- `ViciOne.Ui.Localization` package, update version to `2.35.0`
- `ViciOne.Ui.Shared.Dx` packages, update to version `0.19.0`
- `ViciOne.SystemMonitoring` package, update to version `0.10.0`
- `MailKit` package, version `4.14.1` added
- `Fluid.Core` package, version `2.31.0` added

### Fixed

- `npm`, vulnerabilities fixed
- Fixed synchronization of resolved module versions after complete reset
- Ignore empty files in data protection keys directory
- The time zone selected in the First Run Wizard will now properly be applied to the UI
- Fix display issue of disconnected dialog when diaglog did not disappear on reconnect
- Broken icon placement in navigation tiles

### Removed

- Removed an unnecessary YouTube link and future notice section
- Removed unused font variable `font-weight-semibold`
- Removed `DatabaseSettings` component and `DatabaseConnectionValidator`

## 1.0.0 - 2025-07-25

### Changed

- Reconnection modal, retry logic now uses the highest possible number of retries and establishes the connection as quickly as possible with each attempt

### Removed

- Removed Memory Footprint Logger as it is no longer in use
- Removed dependency to `Blazored.LocalStorage`
- Removed text `Connect your device to the cloud for advanced features like remote monitoring and data analytics.` on Welcome page of First Run Wizard

### Updated

- `HostManagement` packages, update to version `1.0.0`
- `MassTransit` packages, update to version `8.5.1`
- `Microsoft` packages, update to version `9.0.7`
- `System.IO.Abstractions` packages, update to version `22.0.15`
- `ViciOne.Journal` packages, update to version `0.7.0`
- `ViciOne.Suite.Sdk` packages, update to version `1.0.0`
- `ViciOne.Ui.Shared.Dx` packages, update to version `0.15.0`

### Fixed

- When entering network settings too quickly after a restart of the device, "unknown device" will no longer appear

## 0.41.2 - 2025-07-16

### Added

- Automatic module update to latest version on detected sdk incompatibility

### Changed

- Remove `required` keyword from Blazor component parameters annotated with `EditorRequired`
- Replaced `FluentAssertions` with `AwesomeAssertions` version `9.1.0`
- Ensure at least one user keeps `System Admin` role in the system
- The Administrator account will now always have "Admin" roles for all modules

## 0.41.1 - 2025-07-11

### Fixed

- User permissions now correctly reset in the UI when canceling
- User language is now correctly applied when changed
- Roles are now receive the correct permissions after upgrading from previous versions
- Fixed display of module option validation errors on configuration panel

## 0.41.0 - 2025-07-09

### Added

- Support cancellation on preparing WebHost startup
- Set journal page title
- Implement Serilog Enricher for ModuleId
- Implement Journal Sink
- Users can now change their display language and time zone individually in the profile area
- Provide implementation of IArtifactQueryApi for JFrog to allow querying suite artifacts

### Changed

- Redesign parts of system information flyout
- Use systemd filter strings for journal monitoring
- Prohibit using journal view without filter
- Roles Permissions no longer have a "Viewer" AccessLevel
- Users need to authenticate again after swu import or backup restore
- `ModuleLoaderOptions` class, added flag `AllowPreReleases` to control if module pre-releases can be installed on the system

### Removed

- Prohibit publishing of system counts in journal monitoring
- Removed the Mqtt Viewer development feature

### Updated

- Background images replaced with `ViciOne.Suite.Sdk.Client.Components.Wallpaper`
- `bunit` packages, update version to `1.40`
- `DevExpress.Blazor` packages, update to version `24.2.8`
- `ViciOne.Suite.Sdk` packages, update to version `0.31.0`
- `ViciOne.CodeAnalysis` package, update to version `1.2.1`
- `ViciOne.Ui.Blazor.Components` package, update version to `3.8.6`
- `ViciOne.Ui.Localization` package, update to version `2.30.0`
- `ViciOne.Ui.Shared.Dx` packages, update to version `0.14.0`

### Fixed

- `ModuleManagementControlPanel`, fixed display of installed modules when module API is not available

## 0.40.0 - 2025-06-18

### Added

- Settings dialog, Update & restore, System update, added settings for flashing the device

### Fixed

- Settings dialog, network category available again
- `UpdateControlPanel`, fixed available version display
- Connections will now be properly updated in the settings dialog, when a related Tag is changed or deleted

### Updated

- `AspNetCore.SassCompiler` packages, update to version `1.89.2`
- `HostManagement` packages, update to version `0.11.0`
- `Microsoft` packages, update to version `9.0.6`
- `ViciOne.Journal` package, update to version `0.6.0`
- `ViciOne.Ui.Blazor.Components` package, update version to `3.8.5`
- `ViciOne.Ui.Localization` package, update to version `2.29.0`
- `ViciOne.Suite.Sdk` packages, update to version `0.30.4`
- `ViciOne.Ui.MonochromeIcons` packages, update version to `3.6.0`

## 0.39.1 - 2025-06-06

### Fixed

- Settings dialog, network category available again

## 0.39.0 - 2025-06-06

### Changed

- Swapped icon of settings flyout and system information flyout
- Settings dialog opens directly when clicking the cogwheel icon

### Updated

- `ViciOne.SystemMonitoring` package, update to version `0.9.0`

### Fixed

- `MQTT Viewer`, add missing sidebar icon

### Removed

- **Breaking:** Remove internal MQTT broker and its settings

## 0.38.1 - 2025-06-04

### Added

- Detection of software downgrade and data compatibility validation on startup
- Provide client time provider

### Changed

- Set appropriate policy feature flag for journal view page
- Minimum password length is now consistently 12 characters
- The initial 'Administrator' password has been adjusted to 'Pa$$w0rd1234'
- The "Protected"-setting for Tags can no longer be edited by the user
- Apply local time zone to journal view
- Apply local time zone to monitoring chart
- Disable highlighting of `StripComponent` when it is not clickable

### Fixed

- Allow to disable permissions in user control panel
- Render route content after initialize of route component

### Updated

- `ViciOne.Suite.Sdk` packages, update to version `0.30.3`

## 0.38.0 - 2025-05-30

### Added

- Provide implementation for `IControlServiceManagement`
- Download backup containing the system and Core.OS configuration
- Restore backup by direct uploading it from settings section `Upload & Restore`
- Reset suite to factory default from settings section `Upload & Restore`
- Add first run wizard

### Changed

- Use calculation with `MemAvailable` in system monitoring for memory usage
- Rework log center and show statistics of system and suite

### Updated

- `AspNetCore.SassCompiler` packages, update to version `1.89.0`
- `DevExpress.Blazor` packages, update to version `24.2.7`
- `HostManagement.Shared` packages, update to version `0.10.0`
- `MassTransit` packages, update to version `8.4.1`
- `Microsoft` packages, update to version `9.0.5`
- `Riok.Mapperly` packages, update to version `4.2.1`
- `ViciOne.Journal` package, update to version `0.5.0`
- `ViciOne.Suite.Sdk` packages, update to version `0.30.2`
- `ViciOne.Ui.Blazor.Components` package, update version to `3.8.2`
- `ViciOne.Ui.Localization` package, update to version `2.27.0`
- `ViciOne.Ui.Shared.Dx` packages, update to version `0.12.0`
- `ViciOne.Ui.MonochromeIcons` packages, update version to `3.5.0`

### Fixed

- Display spread (min, max) correctly in system monitoring flyout
- Fix version sort when updating modules
- Margins for multi-line headings in flyouts are handled properly
- Testing certificate based mqtt connections now works as intended

## 0.37.0 - 2025-04-30

### Added

- Add cache for `ProcessComponent`
- Show initial values in system monitoring flyout
- Globally accessible tooltip service and display
- Add JFrog api support to be able to download module related information from `https://ifm.jfrog.io`
- Add one hour chart for system monitoring flyout

### Changed

- Update of control panels, navigation tiles, and notification elements when authentication state changes

### Updated

- `AspNetCore.SassCompiler` packages, update to version `1.87.0`
- `DevExpress.Blazor` packages, update to version `24.2.6`
- `HostManagement.Shared` packages, update to version `0.9.0`
- `Microsoft` packages, update to version `9.0.4`
- `System.IO.Abstractions` package, update to version `22.0.14`
- `ViciOne.Suite.Sdk` packages, update to version `0.29.0`
- `ViciOne.SystemMonitoring` package, update to version `0.7.0`
- `ViciOne.Ui.Blazor.Components` package, update version to `3.6.0`
- `ViciOne.Ui.Localization` package, update to version `2.25.0`
- `ViciOne.Ui.MonochromeIcons` packages, update to version `3.4.0`
- `ViciOne.Ui.Shared.Dx` packages, update to version `0.11.1`

### Fixed

- Dispose `IJSObjectReference` safely

## 0.36.0 - 2025-03-26

### Added

- Apply timeout of 10 seconds when testing connections
- Catch errors on creating journal session
- Journal follow session

### Fixed

- Settings dialog, SSH can be activated again when it was disabled before

## 0.35.2 - 2025-03-21

### Fixed

- Creating new users is now possible once again

## 0.35.1 - 2025-03-21

### Changed

- Index page does not display empty navigation tile groups anymore

## 0.35.0 - 2025-03-20

### Added

- Control panels, authorization support
- Navigation tiles, authorization support
- Notification area, authorization support
- Provide journal page on linux
- Catch errors on applying journal filter

### Updated

- `AspNetCore.SassCompiler` packages, update to version `1.86.0`
- `MassTransit` packages, update to version `8.4.0`
- `Microsoft` packages, update to version `9.0.3`
- `Npgsql` packages, update to version `9.0.3`
- `System.IO.Abstractions` package, update to version `22.0.11`
- `ViciOne.Suite.Sdk` packages, update to version `0.28.0`
- `ViciOne.SystemMonitoring` package, update to version `0.4.0`
- `ViciOne.Ui.Blazor.Components` package, update to version `3.4.0`
- `ViciOne.Ui.Localization` package, update to version `2.22.1`
- `ViciOne.Journal` package, update to version `0.2.0`

### Fixed

- Role seeding is not executed on system start anymore

## 0.34.0 - 2025-03-07

### Changed

- Improved communication with HostManagement via named pipe

## 0.33.0 - 2025-03-06

### Changed

- Notification area, help notification element is now visible in DEBUG only

### Updated

- `ViciOne.Suite.Sdk` packages, update to version `0.27.3`
- `ViciOne.Ui.Localization` package, update to version `2.21.1`

## 0.32.0 - 2025-03-04

### Changed

- `HostManagement` communication, added `SystemConfigurationCache` to be configured by option `ConfigurationCacheLifetimeMs` within `HostManagementOptions`

### Updated

- `AspNetCore.SassCompiler` packages, update to version `1.85.1`
- `ViciOne.Ui.Blazor.Components` package, update to version `3.3.2`

## 0.31.0 - 2025-03-03

### Added

- Reset of module workspace option on updating installed versions and on uninstall module
- System monitoring, configured by `SystemMonitoring` section. Enabled by default

### Updated

- `AspNetCore.SassCompiler` packages, update to version `1.85.0`
- `DevExpress.Blazor` packages, update to version `24.2.5`
- `MqttNet` packages, update to version `5.0.1.1416`
- `Npgsql` package, update to version `9.0.3`
- `ViciOne.Suite.Sdk` packages, update to version `0.27.2`
- `ViciOne.Ui.Blazor.Components` package, update to version `3.3.1`
- `ViciOne.Ui.Localization` package, update to version `2.21.0`

## 0.30.0 - 2025-02-17

### Updated

- `ViciOne.Ui.Localization` package, update to version `2.19.0`

## 0.29.0 - 2025-02-14

### Added

- Optional recovery feature to be configured via `InstanceOptions.Recovery`

### Updated

- `Microsoft` packages, update to version `9.0.2`
- `ViciOne.Suite.Sdk` packages, update to version `0.26.4`
- `ViciOne.Ui.Blazor.Components` package, update to version `3.1.1`
- `ViciOne.Ui.Localization` package, update to version `2.18.0`

### Removed

- Removed dependency to `Microsoft.Extensions.Hosting.WindowsServices`
- Removed dependency to `Microsoft.VisualStudio.Azure.Containers.Tools.Targets`

## 0.28.0 - 2025-02-07

### Fixed

- Fixed version display on login page and system&notification flyout
- Fixed delete of orphaned module pre-releases

### Added

- Added new command to control linux services via HostManagement

## 0.27.0 - 2025-02-05

### Added

- Added authorization features for user management

### Changed

- Use in memory outbox only for RabbitMq message setup
- Update module path validation to improve local development support
- Disable `MassTransit` message indention on serialization

### Update

- `AspNetCore.SassCompiler` packages, update to version `1.83.4`
- `bunit` package, update to version `1.38.5`
- `FluentAssertions` packages, update to version `7.1.0`
- `MassTransit` packages, update to version `8.3.5`
- `Microsoft` packages, update to version `9.0.1`
- `Npgsql.EntityFrameworkCore.PostgreSQL` package, update to version `9.0.3`
- `System.IO.Abstractions` package, update to version `21.3.1`
- `ViciOne.Ui.Shared.Dx` packages, update to version `0.8.0`
- `ViciOne.Ui.Blazor.Components` package, update to version `3.1.0`
- `ViciOne.Ui.MonochromeIcons` packages, update to version `3.3.0`
- `ViciOne.Ui.Localization` package, update to version `2.15.0`
- `ViciOne.Suite.Sdk` packages, update to version `0.26.0`
- `xunit` packages, update to version `2.9.3`
- `HostManagement.Shared` packages, update to version `0.7.0`

## 0.26.0 - 2025-01-31

### Added

- Added `SuiteControlService` class, that provides application shutdown feature

### Changed

- Ensure seed of system connection tag if configuration does not contain section `MqttClient`
- DevExpress `DxButton` replaced by own ui-components from blazor.components.
- Hide `Instances` control panel on standalone systems
- Integrate `Tags` control panel into `Connections` control panel as tab page

### Fixed

- Module options have their default values preset on editing settings before installation
- Fix sort pre-release versions on module installation settings
- Fixed usage of local instance information on startup

## 0.25.0 - 2025-01-21

### Added

- Support install/update module ci packages with updated naming

### Changed

- `ModuleLoaderOptions` class, renamed `DisableInstallation` to `AllowInstallation`
- `ViciOne.Ui.Localization` package, update to version `2.4.0`
- `ViciOne.Ui.MonochromeIcons` package, update to version `3.2.0`
- Tag settings reworked

## 0.24.0 - 2024-12-20

### Added

- Add resolve of module version `latest` to highest available version for current SDK
- Ensure module startup errors get logged with details

### Changed

- `.NET` packages, update to version `9.0.0`
- `HostManagement.Shared` packages, update to version `0.6.0`
- `MassTransit` package, update to version `8.3.3`
- `ViciOne.Suite.Sdk` packages, update to version [`0.25.0`](https://gitlab.i40.ifm-datalink.net/acx/vo-suite/vo-suite-sdk/-/blob/master/CHANGELOG.md#0240---2024-12-17)

### Fixed

- Fix module manifest reset after resolving versions a second time

## 0.23.0 - 2024-12-02

### Added

- Provide popup root for Suite client modules

### Changed

- `ViciOne.Suite.Sdk` packages, update to version `0.20.0`
- `ViciOne.Ui.Blazor.Components` package, update to version `2.0.0`
- Replace deleted localization resources from `ViciOne.Suite.Sdk.Localization` with localization resources from `ViciOne.Ui.Localization`

### Fixed

- Settings dialog, prevent signed-in user to delete himself

## 0.22.0 - 2024-11-21

### Fixed

- Fix module dependency validation on startup

## 0.21.0 - 2024-11-19

### Changed

- `DevExpress.Blazor` packages, update to version `24.1.7`
- `HostManagement.Shared` package, update to version `0.5.0`
- `MassTransit` package, update to version `8.3.1`
- `ViciOne.Ui.Shared.Dx` packages, update to version `0.6.0`
- `ViciOne.Suite.Sdk` packages, update to version `0.19.0`

### Removed

- Remove auto extraction of module resource zip files to AppData directory

## 0.20.0 - 2024-11-11

### Changed

- `ViciOne.Ui.Blazor.Components` package, update to version `1.10.1`

### Fixed

- Fix configuration source ordering (overide module options by appsettings, env, or secrets)

## 0.19.0 - 2024-11-08

### Changed

- `ViciOne.Ui.Blazor.Components` package, update to version `1.10.0`

### Fixed

- Fix load module options from json

## 0.18.0 - 2024-11-08

### Added

- New settings "Modules" section added to system settings to allow install/uninstall modules
- Add `ModuleLoader:ManifestSeedPath` option for seeding module manifest at startup

### Changed

- `ViciOne.Ui.Blazor.Components` package, update to version `1.9.1`
- `ViciOne.Ui.MonochromeIcons` package, update to version `2.2.0`

## 0.17.0 - 2024-10-29

### Changed

- `HostManagement.Shared` package, update to version `0.4.0`
- `MassTransit` package, update to version `8.3.0`
- `ViciOne.Suite.Sdk` package, update to version `0.18.0`
- `ViciOne.Ui.Blazor.Components` package, update to version `1.8.0`
- `ViciOne.Ui.MonochromeIcons` package, update to version `2.1.0`

## 0.16.0 - 2024-09-30

### Added

- New "Favorites" category available on the home screen
- Mqtt connections now support certificate based authentication, will messages and other mqtt specific settings

### Changed

- `ViciOne.Ui.ClusterEditor` package, update to version `0.2.2`
- `ViciOne.Ui.MonochromeIcons` package, update to version `1.15.0`
- `ViciOne.Ui.Blazor.Components` package, update to version `1.5.0`
- `IUiModuleBundle` support one module per assembly

## 0.15.0 - 2024-09-03

### Added

- Added SerialNumber to Instance-Information and Instance-Options

### Changed

- `HostManagement` packages, update to version `0.3.0`
- `Sdk.Deployment`, extend module CSS import optimization for `ViciOne.Ui.Blazor.Components`
- `ViciOne.Ui.Shared.Dx` packages, update to version `0.2.0`
- `ViciOne.Ui.MonochromeIcons.Assets` packages, update to version `1.14.0`
- `ViciOne.Ui.ClusterEditor` package, update to version `0.2.0.1313655`

## 0.14.0 - 2024-08-21

### Added

- `Sdk.Client.Components.Settings`, `SettingsFieldSeparator` for visually separating a set of fields

### Changed

- Update `DevExpress.Blazor` to `24.1.5`

## 0.13.0 - 2024-07-24

### Added

- `Sdk.Client`
  - `ControlPanels`
    - `ControlPanelPage` for wrapping content in control panels
    - `IControlPanelCategoryDescriptor` and `InitialControlPanelCategoryAttribute` for control panel categorization
    - `IControlPanelGroupDescriptor` and `InitialControlPanelGroupAttribute` for control panel grouping
    - `IControlPanelDescriptor`, property `Position` for controlling control panel order
- `Sdk.Client.Components`
  - `Settings`
    - `DescriptionBanner` for brief description of content rendered in control panel / control panel page
    - `SettingsLayout`, `SettingsGroup`, `SettingsField`, `SettingsFieldTextBox`, `SettingsFieldButton`, `SettingsStepper` and `SettingsInformation` for use in control panels / control panel pages
    - `ComboBoxExpander` and `SwitchExpander` for use in `SettingsGroup` for expander customization
  - `Switch` for toggling a boolean value
  - `TextBox` for string input

### Changed

- `Sdk.Client`
  - `ControlPanels`
    - `ControlPanelServiceKey`, removed generic parameter `TClientModule`

## 0.12.0 - 2024-06-12

### Added

- `Sdk.Client`
  - Support configuration of notification element order
  - Support TypeScript

### Changed

- `Sdk.Client`
  - Removed obsolete `NotificationBarElement` and dependencies
  - Removed obsolete `AddDxAllResources`
  - Removed obsolete code from `IBrowserLocalStorageService`

## 0.11.0 - 2024-04-10

### Added

- Support platform specific Module and FunctionBlock packages

### Changed

- `Sdk.Deployment` mandatory platform parameter was added to publish script

## 0.10.0 - 2024-02-27

### Added

- `Sdk.Localization` is published as NuGet package `ViciOne.Suite.Sdk.Localization` from now on

## 0.9.0 - 2024-02-19

### Added

- `Sdk`, `MasterHealthInfoChanged` sealed and parameter `type` removed
- `Sdk.Client`
  - `_typography.scss` added
  - `NotificationArea` feature (`NotificationBarElement` is obsolete)
- `Sdk.Client.Components`, `MaterialDesignIconComponent` added
- `Sdk.Localization`
  - `CommonVocabulary`, `Copy` added
  - `UserActions` added
- `Sdk.Testing`, `RandomExtensions` added

### Changed

- `Sdk.Client`
  - `IControlPanelRegistry<>` inherits from `IRegistry<>`
  - more robust service registration using keyed services

## 0.8.0 - 2024-02-06

### Added

- Manual edit of MQTT-DataPort and its tree using ClusterEditor

### Changed

- Removed `Async` suffix from SDK api
- Updated engine host to `0.13.0`

### Removed

- `ControlPanelElement` support

## 0.7.0 - 2024-01-19

### Added

- `ControlPanel` feature (`ControlPanelElement` is obsolete)
- CSS post-processing during client module deployment (removal of `@import` statements targeting SDK CSS bundles)

### Removed

- `IBackgroundTaskQueue` Support - replaced by HostedService

## 0.6.0 - 2023-12-14

### Changed

- Removed obsolete methods from SDK
- Updated target framework to .NET8

## 0.5.0 - 2023-12-07

### Changed

- Keep libraries for runtime `win` on module deployment

### Added

- `NavTile` feature (`NavItem` is obsolete)

## 0.4.0 - 2023-11-21

### Added

- Support Http connection
- Support Azure.Iot.Hub connection

### Changed

- Remove unsupported languages on module deployment

## 0.3.0 - 2023-11-06

### Added

- Property `Localizer` in `ControlPanelElement`
- Property `Localizer` in `NotificationBarElement`

### Changed

- `ClientModule`, property `LocalizationProvider` renamed to `Localizer`
- `NavItem`, property `LocalizationProvider` renamed to `Localizer`
- `BackendModule`, property `Version` removed

## 0.2.0 - 2023-10-27

### Added

- Provide shared `scss` files via `Sdk.Client` package
- `Sdk.Deployment` package with support scripts
- Property `LocalizationProvider` in `ClientModule` and `NavItem`

## 0.1.0 - 2023-10-16

- Initial Release
