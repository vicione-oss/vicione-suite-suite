using Core.Shared.UserManagement.Contracts;
using Sdk.Messaging;

namespace Core.Shared.UserManagement.Requests;

public sealed record GetUsersResponse(List<UserProfile> Users, ErrorInfo? RequestError = null) : IResponse;