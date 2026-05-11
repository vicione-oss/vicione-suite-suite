using Core.OS.DbContext;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Messaging;
using Sdk.Connections.Events;
using Sdk.Connections.Requests;

namespace Core.OS.Connections.Consumers;

public sealed partial class GetTagsConsumer(IConnectionDbContext dbContext, ILogger<GetTagsConsumer> logger) :
    RequestConsumer<GetTags, GetTagsResponse>
{
    public override async Task<GetTagsResponse> Respond(GetTags message, CancellationToken cancellationToken)
    {
        var result = await dbContext.Tags.ToListAsync(cancellationToken);
        return new GetTagsResponse(result);
    }

    public override Task<GetTagsResponse> HandleException(GetTags message, Exception e, CancellationToken cancellationToken)
    {
        LogError(logger, e);

        return Task.FromResult(new GetTagsResponse([], new(TagErrorCodes.UnknownError, e.Message)));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to get connection tags")]
    private static partial void LogError(ILogger<GetTagsConsumer> logger, Exception ex);
}
