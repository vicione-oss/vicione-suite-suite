using Sdk.Messaging;

namespace Core.Shared.Persistence.Requests;

public sealed record GetBackup(string? FileName = null) : IRequest<GetBackupResponse>;
