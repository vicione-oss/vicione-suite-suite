using Sdk.Messaging;

namespace Core.Shared.UserManagement.Requests;

public sealed record GetRolesResponse(List<string> Roles, ErrorInfo? RequestError = null) : IResponse;