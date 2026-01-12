using Core.Shared.UserManagement.Contracts;
using MassTransit;
using Sdk.Messaging;

namespace Core.Shared.UserManagement.Events;

[ForwardToUI]
public sealed record UserErrorEvent(Guid CorrelationId, ErrorInfo ErrorInfo, UserName Username) : IEvent, CorrelatedBy<Guid>
{
    public const int UnknownError = 0;
    public const int NotFound = 1;
    public const int CreateFailed = 100;
    public const int CreateFailedAlreadyExists = 101;
    public const int CreateFailedMissingPw = 102;
    public const int UpdateFailed = 200;
    public const int UpdateFailedNotFound = 201;
    public const int UpdateFailedPassword = 202;
    public const int DeleteFailed = 300;
    public const int DeleteFailedNotFound = 301;
    public const int SystemAdminLockout = 401;
}
