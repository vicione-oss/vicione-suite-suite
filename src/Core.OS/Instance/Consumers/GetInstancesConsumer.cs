using Core.OS.DbContext;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Requests;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Messaging;

namespace Core.OS.Instance.Consumers;

public sealed class GetInstancesConsumer(IApplicationDbContext dbContext, ILogger<GetInstancesConsumer> logger) : RequestConsumer<GetInstances, GetInstancesResponse>
{
    private readonly IApplicationDbContext _dbContext = dbContext;
    private readonly ILogger<GetInstancesConsumer> _logger = logger;

    protected override async Task<GetInstancesResponse> Respond(ConsumeContext<GetInstances> context)
    {
        List<InstanceInformation>? instanceInfos = null;

        if (context.Message.InstanceId is null)
        {
            instanceInfos = await _dbContext
                .InstanceInfo
                .AsNoTracking()
                .ToListAsync(context.CancellationToken);
        }
        else
        {
            var info = await _dbContext
                .InstanceInfo
                .AsNoTracking()
                .SingleOrDefaultAsync(i => i.Id == context.Message.InstanceId, context.CancellationToken);
            if (info is not null)
                instanceInfos = [info];
        }

        return new GetInstancesResponse(instanceInfos ?? []);
    }

    protected override Task<GetInstancesResponse> HandleException(ConsumeContext<GetInstances> context, Exception e)
    {
        _logger.LogError(e, $"Failed to handle {nameof(GetInstances)}");

        return Task.FromResult(new GetInstancesResponse([], new(0, e.Message)));
    }
}
