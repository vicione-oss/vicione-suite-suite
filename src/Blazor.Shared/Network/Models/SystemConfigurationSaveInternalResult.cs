using HostManagement.Shared.Contracts;

namespace Blazor.Shared.Network.Models;

internal readonly record struct SystemConfigurationSaveInternalResult(SystemConfiguration ProposedSystemConfiguration, string Message = "")
    : ISaveInternalResult;
