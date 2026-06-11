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

public sealed partial class DbChangeSetConsumer(IServiceProvider services, ILogger<DbChangeSetConsumer> logger) : IConsumer<DbChangeSet>
{
    public async Task Consume(ConsumeContext<DbChangeSet> context)
    {
        var dbContext = GetChangeSetDbContext(context.Message.ContextType);
        if (dbContext is null)
            return;

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
                {
                    var existing = await dbContext.FindAsync(entityType, GetPrimaryKeyValues(dbContext, entity, entityType), context.CancellationToken);
                    if (existing is not null)
                        dbContext.Remove(existing);
                    break;
                }

                case EntityState.Modified:
                case EntityState.Added:
                {
                    var existing = await dbContext.FindAsync(entityType, GetPrimaryKeyValues(dbContext, entity, entityType), context.CancellationToken);
                    if (existing is not null)
                        dbContext.Entry(existing).CurrentValues.SetValues(entity);
                    else
                        dbContext.Add(entity);
                    FixConnectionRelation(dbContext);
                    break;
                }

                default:
                    throw new InvalidEnumArgumentException(nameof(change.State), (int)change.State, typeof(EntityState));
            }
        }

        try
        {
            await dbContext.SaveChangesAsync(context.CancellationToken);
        }
        catch (DbUpdateException e)
        {
            LogApplyChangesFailed(logger, e, context.Message.Changes.Count, context.Message.ContextType);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to apply {MessageCount} changes to {ContextType}")]
    private static partial void LogApplyChangesFailed(ILogger logger, Exception ex, int messageCount, string contextType);

    private static object?[] GetPrimaryKeyValues(Microsoft.EntityFrameworkCore.DbContext dbContext, object entity, Type entityType)
    {
        var efEntityType = dbContext.Model.FindEntityType(entityType)
            ?? throw new InvalidOperationException($"Entity type '{entityType.FullName}' is not part of the model for context '{dbContext.GetType().Name}'");

        var primaryKey = efEntityType.FindPrimaryKey()
            ?? throw new InvalidOperationException($"Entity type '{entityType.FullName}' has no primary key defined");

        return primaryKey.Properties
            .Select(p => p.PropertyInfo?.GetValue(entity) ?? p.FieldInfo?.GetValue(entity))
            .ToArray();
    }

    private static void FixConnectionRelation(Microsoft.EntityFrameworkCore.DbContext dbContext)
    {
        // what happens on creating a connection with tag is that 2 changes got tracked and published
        // after connection got added change tracker has 3 changes already:
        // [0] connection added
        // [1] tag added    (ALREADY EXISTS!)
        // [2] connection tag added

        // if adding an entity that's already tracked needs special treatment 
        var tagsAdded = dbContext.ChangeTracker.Entries().Where(e => e is { State: EntityState.Added, Entity: Tag });
        foreach (var entry in tagsAdded)
        {
            if (entry.Entity is not Tag tag)
                continue;

            // ensure that already existing tags don't get added again
            if (dbContext.Find<Tag>(tag.Id) is not null)
                entry.State = EntityState.Unchanged;
        }

        // dbContext.Find<ConnectionTag>(connectionTag.ConnectionId, connectionTag.TagId)
        // should return null if the connection tag does not exist, but it returns an entity
        // with state unchanged that does not exist in the database.
        // to ensure we really get the existing item dbset is used with SingleOrDefault
        var connectionTagSet = dbContext.Set<ConnectionTag>();
        var cleanedUp = false;

        // modification of a connection triggers relation change - skip if existing                
        var connectionTagsAdded = dbContext.ChangeTracker.Entries().Where(e => e is { State: EntityState.Added, Entity: ConnectionTag });
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
            allContextTypeInfos = services.GetServices<ModuleContextTypeInformation>();
        }
        catch (InvalidOperationException)
        {
            return null;//nothing to do (no registered Context - can happen in unit testing)
        }

        var ctxType = allContextTypeInfos.SingleOrDefault(c
                => c.FullName.Equals(contextType, StringComparison.OrdinalIgnoreCase))?.ContextType;
        if (ctxType is null)
            return null;

        return services.GetService(ctxType) as Microsoft.EntityFrameworkCore.DbContext;
    }
}
