using Core.OS.DbContext;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Requests;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Messaging;

namespace Core.OS.Instance.Consumers;

public sealed class GetCrossInstanceConfigurationConsumer(IApplicationDbContext dbContext, ILogger<GetCrossInstanceConfigurationConsumer> logger)
    : RequestConsumer<GetCrossInstanceConfiguration, GetCrossInstanceConfigurationResponse>
{
    protected override async Task<GetCrossInstanceConfigurationResponse> Respond(ConsumeContext<GetCrossInstanceConfiguration> context)
    {
        var crossInstanceConfiguration = await dbContext
            .CrossInstanceConfiguration
            .AsNoTracking()
            .SingleOrDefaultAsync(context.CancellationToken) ?? new CrossInstanceConfiguration();

        return new GetCrossInstanceConfigurationResponse(crossInstanceConfiguration);
    }

    protected override Task<GetCrossInstanceConfigurationResponse> HandleException(ConsumeContext<GetCrossInstanceConfiguration> context, Exception e)
    {
        logger.LogError(e, $"Failed to handle {nameof(GetCrossInstanceConfiguration)}");

        return Task.FromResult(new GetCrossInstanceConfigurationResponse(new(), new(0, e.Message)));
    }
}
