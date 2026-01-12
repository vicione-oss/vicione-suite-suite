using Core.OS.DbContext;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using Sdk.Connections.Extensions;
using Sdk.Connections.Requests;
using Sdk.Messaging;

namespace Core.OS.Connections.Consumers;

public sealed class GetConnectionsConsumer(IConnectionDbContext dbContext, ILogger<GetConnectionsConsumer> logger) : RequestConsumer<GetConnections, GetConnectionsResponse>
{
    protected override async Task<GetConnectionsResponse> Respond(ConsumeContext<GetConnections> context)
    {
        var result = new List<Connection>();

        logger.LogDebug("Consume {RequestName} CorrelationId:{CorrelationId} ConnectionId:{ConnectionId}",
            nameof(GetConnections),
            context.CorrelationId,
            context.Message.ConnectionId);

        if (context.Message.ConnectionId is not null)
        {
            var connection = await dbContext.Connections
                .Include(c => c.Tags)
                .AsNoTracking()
                .SingleOrDefaultAsync(node => node.Id.Equals(context.Message.ConnectionId), context.CancellationToken);

            if (connection is not null)
                result.Add(connection);
        }
        else if (context.Message.FilterTypeNames is { Count: > 0 })
        {
            // .Where(c => __request_FilterTypeNames_0.Contains(c.Type.Name))' could not be translated
            // this was working in test!!
            var connections = await dbContext.Connections
                .Include(c => c.Tags)
                .AsNoTracking()
                .ToListAsync(context.CancellationToken);

            result.AddRange(connections
                .Where(k => context.Message.FilterTypeNames.Contains(k.Type.Name))
                .ToList());
        }
        else
        {
            result.AddRange(await dbContext.Connections
                .Include(c => c.Tags)
                .AsNoTracking()
                .ToListAsync(context.CancellationToken));
        }

        if (context.Message.RequiredTags is not null)
            result = result.Where(c => !context.Message.RequiredTags.Except(c.Tags).Any()).ToList();

        if (context.Message.InstanceId is not null)
            result = result.FindInstanceConnections(context.Message.InstanceId.Value).ToList();

        return new GetConnectionsResponse(result);
    }

    protected override Task<GetConnectionsResponse> HandleException(ConsumeContext<GetConnections> context, Exception e)
    {
        logger.LogError(e, $"Failed to handle {nameof(GetConnections)}");

        return Task.FromResult(new GetConnectionsResponse([], new(ConnectionErrorOccured.UnknownError, e.Message)));
    }
}
