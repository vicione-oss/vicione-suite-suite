using Sdk.Authorization;

namespace Blazor.Shared.Tests.Models;

public readonly record struct ModuleAuthorizationClaim(string ModuleId, AccessLevel AccessLevel, string FeatureName);
