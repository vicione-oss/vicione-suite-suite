namespace Blazor.Shared.UserManagement.Contracts;

/// <summary>
/// Describes the result of a failed call to a method of usermanagement services
/// </summary>
public readonly record struct UserManagementServiceErrorResult(string ErrorMessage, int? ErrorCode = null) : IUserManagementServiceResult;
