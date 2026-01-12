using System.Diagnostics.CodeAnalysis;
using Sdk.Messaging;

namespace Core.Shared.Persistence.Requests;

[SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Must be serializable")]
public sealed record GetBackupResponse(string FileName, byte[]? Content, ErrorInfo? RequestError = null) : IResponse;
