using Core.OS.Connections.Mqtt;
using Core.OS.Instance;
using Core.OS.Logging;
using Core.OS.MessageBus.MassTransit.Configuration;
using Core.OS.UserManagement.Configuration;
using Core.Shared.UserManagement.Configuration;
using Microsoft.Extensions.Options;

namespace Core.OS.Configuration;

// Every options type bound to a configuration section by AddValidatedOptions gets a validator here, and
// AddValidatedOptions cannot be called without one - see Core.OS.Extensions.IServiceCollectionExtensions.
//
// The validators live in Core.OS rather than beside their options types because the [OptionsValidator]
// source generator ships in the ASP.NET Core reference pack
// (packs/Microsoft.AspNetCore.App.Ref/<version>/analyzers/dotnet/cs/Microsoft.Extensions.Options.SourceGeneration.dll)
// and Core.OS is the only project referencing that framework - Core.Shared, Core.Module and Core.Artifacts
// build against Microsoft.NET.Sdk, where the generator is not available. The generator reads the public
// members of the options types across the assembly boundary, so their location does not matter.
//
// Unlike ValidateDataAnnotations(), which only ever looks at the top-level properties, these validators
// recurse through every property carrying [ValidateObjectMembers] / [ValidateEnumeratedItems], which is
// what makes the [Range] bounds on the nested settings classes actually reject.
//
// A type with no validation attributes anywhere in its graph cannot have a validator: the generator
// reports SYSLIB1203 and emits no Validate method, so the partial class fails to compile. Those types
// are registered through AddUnvalidatedSuiteOptions instead, which records why - see UnvalidatedSuiteOptions.

[OptionsValidator]
internal sealed partial class InstanceOptionsValidator : IValidateOptions<InstanceOptions>;

[OptionsValidator]
internal sealed partial class UserManagementOptionsValidator : IValidateOptions<UserManagementOptions>;

[OptionsValidator]
internal sealed partial class MessageBusOptionsValidator : IValidateOptions<MessageBusOptions>;

[OptionsValidator]
internal sealed partial class LoggingOptionsValidator : IValidateOptions<LoggingOptions>;

[OptionsValidator]
internal sealed partial class ExternalIdProviderOptionsValidator : IValidateOptions<ExternalIdProviderOptions>;

[OptionsValidator]
internal sealed partial class MqttClientOptionsValidator : IValidateOptions<MqttClientOptions>;
