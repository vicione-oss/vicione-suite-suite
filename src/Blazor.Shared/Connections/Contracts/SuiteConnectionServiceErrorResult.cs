using Blazor.Shared.Connections.Services;

namespace Blazor.Shared.Connections.Contracts;

/// <summary>
/// Describes the result of a failed call to a method of <see cref="ISuiteConnectionService"/>
/// </summary>
public readonly record struct SuiteConnectionServiceErrorResult(string ErrorMessage, int? ErrorCode = null)
    : ISuiteConnectionServiceResult;
