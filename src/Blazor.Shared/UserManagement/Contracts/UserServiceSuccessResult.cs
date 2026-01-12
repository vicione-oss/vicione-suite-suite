using Blazor.Shared.UserManagement.Services;

namespace Blazor.Shared.UserManagement.Contracts;

/// <summary>
/// Describes the result of a successful call to a method of <see cref="IUserService"/> 
/// </summary>
public readonly record struct UserServiceSuccessResult() : IUserServiceResult;
