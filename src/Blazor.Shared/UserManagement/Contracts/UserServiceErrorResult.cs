using Blazor.Shared.UserManagement.Services;

namespace Blazor.Shared.UserManagement.Contracts;

/// <summary>
/// Describes the result of a failed call to a method of <see cref="IUserService"/> 
/// </summary>
public readonly record struct UserServiceErrorResult(string ErrorMessage, int? ErrorCode = null) : IUserServiceResult;
