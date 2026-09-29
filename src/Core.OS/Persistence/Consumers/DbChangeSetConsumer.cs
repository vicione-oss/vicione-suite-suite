using System.ComponentModel;
using System.Data;
using System.Text.Json;
using Core.OS.Instance;
using Core.OS.Instance.Commands;
using Core.OS.Instance.Contracts;
using Core.OS.Instance.Mappers;
using Core.OS.Instance.Services;
using Core.OS.Persistence.Extensions;
using MassTransit;
using MassTransit.Configuration;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Messaging;
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

public sealed partial class DbChangeSetConsumer(
    IServiceProvider services,
    ILogger<DbChangeSetConsumer> logger,
    ReplicationSequenceTracker sequenceTracker,
    ReplicationLagTracker lagTracker,
    ILocalInstanceInformationProvider localInstanceInfo,
    SynchronizationState synchronizationState) : IConsumer<DbChangeSet>
{
    public async Task Consume(ConsumeContext<DbChangeSet> context)
    {
        await synchronizationState.Ready;

        var message = context.Message;
        var result = sequenceTracker.Submit(message.ContextType, message.SequenceNumber, message);

        foreach (var changeSet in result.MessagesToApply)
        {
            await ApplyChangeSet(changeSet, context.CancellationToken);
            lagTracker.Record(changeSet.ContextType, changeSet.PublishedAt);
        }

        if (result.RequiresFullSync)
        {
            LogBufferFlushed(logger, message.ContextType, result.MessagesToApply.Count);
            await TriggerFullSync(context);
        }
    }

    private async Task ApplyChangeSet(DbChangeSet changeSet, CancellationToken cancellationToken)
    {
        var dbContext = GetChangeSetDbContext(changeSet.ContextType);
        if (dbContext is null)
            return;

        var appliedEntityTypes = new HashSet<string>(StringComparer.Ordinal);
        Exception? lastEntityException = null;

        foreach (var change in changeSet.Changes)
        {
            try
            {
                ApplyEntity(dbContext, change);
                appliedEntityTypes.Add(change.EntityTypeFullName);
            }
            catch (Exception ex)
            {
                lastEntityException = ex;
                LogEntityApplyFailed(logger, ex, change.EntityTypeFullName, change.State.ToString(), changeSet.ContextType);
            }
        }

        if (appliedEntityTypes.Count == 0 && lastEntityException is not null)
            throw new InvalidOperationException(
                $"All {changeSet.Changes.Count} entities in batch for '{changeSet.ContextType}' failed to apply. See previous log entries for details.",
                lastEntityException);

        if (appliedEntityTypes.Count == 0)
            return;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e)
        {
            LogApplyChangesFailed(logger, e, changeSet.Changes.Count, changeSet.ContextType);
            throw;
        }

        services.NotifyReplicationObservers(logger,
            observer => observer.ChangeSetApplied(changeSet.ContextType, appliedEntityTypes));
    }

    private static void ApplyEntity(Microsoft.EntityFrameworkCore.DbContext dbContext, ChangedEntity change)
    {
        if (change.Entity is null)
            throw new NoNullAllowedException("Must be prevented during publish!");

        var entityType = EntityTypeCache.GetOrAdd(change.EntityTypeFullName, change.AssemblyFullName);
        var entity = JsonSerializer.Deserialize(change.Entity, entityType, DefaultJsonSerializerSettings.Default)
            ?? throw new InvalidOperationException($"Deserialization of '{change.EntityTypeFullName}' returned null.");

        switch (change.State)
        {
            case EntityState.Detached:
            case EntityState.Unchanged:
                break;

            case EntityState.Deleted:
            {
                var existing = dbContext.Find(entityType, GetPrimaryKeyValues(dbContext, entity, entityType));
                if (existing is not null)
                    dbContext.Remove(existing);
                break;
            }

            case EntityState.Modified:
            case EntityState.Added:
            {
                var existing = dbContext.Find(entityType, GetPrimaryKeyValues(dbContext, entity, entityType));
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

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to apply {MessageCount} changes to {ContextType}")]
    private static partial void LogApplyChangesFailed(ILogger logger, Exception ex, int messageCount, string contextType);

    [LoggerMessage(Level = LogLevel.Error, Message = "Replication entity skipped: failed to apply entity '{EntityType}' (state: {EntityState}) in context '{ContextType}'. This entity will remain divergent until a full-sync is triggered.")]
    private static partial void LogEntityApplyFailed(ILogger logger, Exception ex, string entityType, string entityState, string contextType);

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
        // Creating a connection with a tag tracks and publishes two changes.
        // after connection got added change tracker has 3 changes already:
        // [0] connection added
        // [1] tag added    (ALREADY EXISTS!)
        // [2] connection tag added

        // Adding an already tracked entity needs special treatment.
        var tagsAdded = dbContext.ChangeTracker.Entries().Where(e => e is { State: EntityState.Added, Entity: Tag });
        foreach (var entry in tagsAdded)
        {
            if (entry.Entity is not Tag tag)
                continue;

            // An existing tag must not be added a second time.
            if (dbContext.Find<Tag>(tag.Id) is not null)
                entry.State = EntityState.Unchanged;
        }

        // Should return null when the connection tag does not exist, but it returns an entity.
        // with state unchanged that does not exist in the database.
        // to ensure we really get the existing item dbset is used with SingleOrDefault
        var connectionTagSet = dbContext.Set<ConnectionTag>();
        var cleanedUp = false;

        // Modifying a connection triggers a relation change, which is skipped when it exists.
        var connectionTagsAdded = dbContext.ChangeTracker.Entries().Where(e => e is { State: EntityState.Added, Entity: ConnectionTag });
        foreach (var entry in connectionTagsAdded)
        {
            if (entry.Entity is not ConnectionTag connectionTag)
                continue;

            if (!cleanedUp)
            {
                // A modified tag arrives as a remove followed by an add.
                // if connection had 2 tags and 1 was removed we get 1 added (0->1) here!
                // so we remove all 'old' connection tags except the added one
                var existing = connectionTagSet.Where(k => k.ConnectionId == connectionTag.ConnectionId && k.TagId != connectionTag.TagId);
                foreach (var existingEntry in existing)
                {
                    connectionTagSet.Remove(existingEntry);
                }
                cleanedUp = true;
            }

            // An existing tag must not be added a second time.
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

    private async Task TriggerFullSync(ConsumeContext<DbChangeSet> context)
    {
        // Close the gate — block subsequent messages until full-sync completes
        synchronizationState.Reset();

        var instanceInfo = localInstanceInfo.Local;
        var endPoint = await context.GetSendEndpoint(MessagingHelper.GetCommandEndpointAddress<RegisterInstance>());
        await endPoint.Send(instanceInfo.ToRegisterInstanceCommand(
            [.. localInstanceInfo.LoadedModules],
            [],
            sequenceTracker.GetAllLastApplied()
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)) with { ForceSync = true }, context.CancellationToken);

        // Reset tracker so the next message after full-sync completes is accepted
        sequenceTracker.Reset();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Replication buffer timed out for {ContextType}: flushing {Count} buffered messages and triggering full-sync.")]
    private static partial void LogBufferFlushed(ILogger logger, string contextType, int count);
}
