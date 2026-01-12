using Core.OS.DbContext;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sdk.Connections.Events;
using Sdk.Connections.Requests;
using Sdk.Messaging;

namespace Core.OS.Connections.Consumers;

public sealed class GetTagsConsumer(IConnectionDbContext dbContext, ILogger<GetTagsConsumer> logger) :
    RequestConsumer<GetTags, GetTagsResponse>
{
    protected override async Task<GetTagsResponse> Respond(ConsumeContext<GetTags> context)
    {
        logger.LogDebug("Consume {RequestName} CorrelationId:{CorrelationId}", nameof(GetTags), context.CorrelationId);

        var result = await dbContext.Tags.ToListAsync(context.CancellationToken);
        return new GetTagsResponse(result);
    }

    protected override Task<GetTagsResponse> HandleException(ConsumeContext<GetTags> context, Exception e)
    {
        logger.LogError(e, $"Failed to handle {nameof(GetTags)}");

        return Task.FromResult(new GetTagsResponse([], new(ConnectionErrorOccured.UnknownError, e.Message)));
    }
}
