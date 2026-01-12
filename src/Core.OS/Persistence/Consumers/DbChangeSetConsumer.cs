using System.ComponentModel;
using System.Data;
using System.Text.Json;
using MassTransit;
using MassTransit.Configuration;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Persistence;
using Sdk.Connections.Contracts;
using Sdk.Messaging;

namespace Core.OS.Persistence.Consumers;

public sealed class DbChangeSetConsumerDefinition : ConsumerDefinition<DbChangeSetConsumer>
{
    public DbChangeSetConsumerDefinition()
    {
        EndpointDefinition
            = new ConsumerEndpointDefinition<DbChangeSetConsumer>(
                new EndpointSettings<IEndpointDefinition<DbChangeSetConsumer>> { PrefetchCount = 1 });
        ConcurrentMessageLimit = 1;
    }
}

public sealed class DbChangeSetConsumer(IServiceProvider services, ILogger<DbChangeSetConsumer> logger) : IConsumer<DbChangeSet>
{
    private readonly IServiceProvider _services = services;
    private readonly ILogger<DbChangeSetConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<DbChangeSet> context)
    {
        var dbContext = GetChangeSetDbContext(context.Message.ContextType);
        if (dbContext is null)
            return;

        try
        {
            foreach (var change in context.Message.Changes)
            {
                if (change.Entity is null)
                    throw new NoNullAllowedException("Must be prevented during publish!");

                var entityType = EntityTypeCache.GetOrAdd(change.EntityTypeFullName, change.AssemblyFullName);
                var entity = JsonSerializer.Deserialize(change.Entity, entityType, DefaultJsonSerializerSettings.Default);
                if (entity is null)
                    continue;

                switch (change.State)
                {
                    case EntityState.Detached:
                    case EntityState.Unchanged:
                        break;

                    case EntityState.Deleted:
                        dbContext.Remove(entity);
                        break;

                    case EntityState.Modified:
                        dbContext.Update(entity);
                        FixConnectionRelation(dbContext);
                        break;

                    case EntityState.Added:
                        dbContext.Add(entity);
                        FixConnectionRelation(dbContext);
                        break;

                    default:
                        throw new InvalidEnumArgumentException(nameof(change.State), (int)change.State, typeof(EntityState));
                }
            }

            // todo: issue #466
            await dbContext.SaveChangesAsync(context.CancellationToken);
        }
        catch (DbUpdateException e)// skip retry in that case
        {
            _logger.LogError(e,
                "Failed to apply {MessageCount} changes to {ContextType}",
                context.Message.Changes.Count,
                context.Message.ContextType);

            LogChangeErrors(context.Message.ContextType, context.Message.Changes);
        }
        catch (InvalidOperationException e)// skip retry in that case
        {
            _logger.LogError(e,
                "Invalid operation applying {MessageCount} changes to {ContextType}",
                context.Message.Changes.Count,
                context.Message.ContextType);

            LogChangeErrors(context.Message.ContextType, context.Message.Changes);
        }
    }

    private static void FixConnectionRelation(Microsoft.EntityFrameworkCore.DbContext dbContext)
    {
        // what happens on creating a connection with tag is that 2 changes got tracked and published
        // after connection got added change tracker has 3 changes already:
        // [0] connection added
        // [1] tag added    (ALREADY EXISTS!)
        // [2] connectiontag added

        // if adding a entity that's already tracked needs special treatment 
        var tagsAdded = dbContext.ChangeTracker.Entries().Where(e => e.State == EntityState.Added && e.Entity is Tag);
        foreach (var entry in tagsAdded)
        {
            if (entry.Entity is not Tag tag)
                continue;

            // ensure that already existing tags don't get added again
            if (dbContext.Find(typeof(Tag), tag.Id) is not null)
                entry.State = EntityState.Unchanged;
        }

        // dbContext.Find<ConnectionTag>(connectionTag.ConnectionId, connectionTag.TagId)
        // should return null if the connection tag does not exist but it returns an entity
        // with state unchanged that does not exist in the database.
        // to ensure we really get the existing item dbset is used with SingleOrDefault
        var connectionTagSet = dbContext.Set<ConnectionTag>();
        var cleanedUp = false;

        // modification of a connection triggers relation change - skip if existing                
        var connectionTagsAdded = dbContext.ChangeTracker.Entries().Where(e => e.State == EntityState.Added && e.Entity is ConnectionTag);
        foreach (var entry in connectionTagsAdded)
        {
            if (entry.Entity is not ConnectionTag connectionTag)
                continue;

            if (!cleanedUp)
            {
                // if a tag was modified it's not modified but removed and added again
                // if connection had 2 tags and 1 was removed we get 1 added (0->1) here!  
                // so we remove all 'old' connection tags except the added one
                var existing = connectionTagSet.Where(k => k.ConnectionId == connectionTag.ConnectionId && k.TagId != connectionTag.TagId);
                foreach (var existingEntry in existing)
                {
                    connectionTagSet.Remove(existingEntry);
                }
                cleanedUp = true;
            }

            // ensure that already existing tags don't get added again
            var alreadyAdded = connectionTagSet.SingleOrDefault(k => k.ConnectionId == connectionTag.ConnectionId && k.TagId == connectionTag.TagId);
            if (alreadyAdded is not null)
                entry.State = EntityState.Unchanged;
        }
    }

    private Microsoft.EntityFrameworkCore.DbContext? GetChangeSetDbContext(string contextType)
    {
        IEnumerable<ModuleContextTypeInformation> allContextTypeInfos;
        try
        {
            allContextTypeInfos = _services.GetServices<ModuleContextTypeInformation>();
        }
        catch (InvalidOperationException)
        {
            return null;//nothing to do (no registered Context - can happen in unit testing)
        }

        var ctxType = allContextTypeInfos.SingleOrDefault(c
                => c.FullName.Equals(contextType, StringComparison.OrdinalIgnoreCase))?.ContextType;
        if (ctxType is null)
            return null;

        return _services.GetService(ctxType) is not Microsoft.EntityFrameworkCore.DbContext dbContext ? null : dbContext;
    }

    private void LogChangeErrors(string contextType, IEnumerable<ChangedEntity> changes)
    {
        foreach (var change in changes)
            _logger.LogError("{ContextType}[{State}]: Failed on entity {Entity}",
                contextType,
                change.State,
                change.Entity);
    }
}
