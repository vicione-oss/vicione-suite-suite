using Core.Shared.UserManagement.Contracts;
using Sdk.Messaging;

namespace Core.Shared.UserManagement.Requests;

public sealed record GetUsers(UserName? UserName = null) : IRequest<GetUsersResponse>;
