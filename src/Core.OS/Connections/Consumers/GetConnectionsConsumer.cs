using Core.OS.DbContext;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Messaging;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using Sdk.Connections.Extensions;
using Sdk.Connections.Requests;

namespace Core.OS.Connections.Consumers;

public sealed partial class GetConnectionsConsumer(IConnectionDbContext dbContext, ILogger<GetConnectionsConsumer> logger) : RequestConsumer<GetConnections, GetConnectionsResponse>
{
    public override async Task<GetConnectionsResponse> Respond(GetConnections message, CancellationToken cancellationToken)
    {
        var result = new List<Connection>();

        if (message.ConnectionId is not null)
        {
            var connection = await dbContext.Connections
                .Include(c => c.Tags)
                .AsNoTracking()
                .SingleOrDefaultAsync(node => node.Id.Equals(message.ConnectionId), cancellationToken);

            if (connection is not null)
                result.Add(connection);
        }
        else if (message.FilterTypeNames is { Count: > 0 })
        {
            // .Where(c => __request_FilterTypeNames_0.Contains(c.Type.Name))' could not be translated
            // this was working in test!!
            var connections = await dbContext.Connections
                .Include(c => c.Tags)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            result.AddRange(connections
                .Where(k => message.FilterTypeNames.Contains(k.Type.Name))
                .ToList());
        }
        else
        {
            result.AddRange(await dbContext.Connections
                .Include(c => c.Tags)
                .AsNoTracking()
                .ToListAsync(cancellationToken));
        }

        if (message.RequiredTags is not null)
            result = result.Where(c => !message.RequiredTags.Except(c.Tags).Any()).ToList();

        if (message.InstanceId is not null)
            result = result.FindInstanceConnections(message.InstanceId.Value).ToList();

        return new GetConnectionsResponse(result);
    }

    public override Task<GetConnectionsResponse> HandleException(GetConnections message, Exception e, CancellationToken cancellationToken)
    {
        LogError(logger, e);

        return Task.FromResult(new GetConnectionsResponse([], new(ConnectionErrorCodes.UnknownError, e.Message)));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to get connections")]
    private static partial void LogError(ILogger<GetConnectionsConsumer> logger, Exception ex);
}
