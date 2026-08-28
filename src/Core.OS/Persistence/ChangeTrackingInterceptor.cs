using System.Collections;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Sdk.Backend.Persistence;
using Sdk.Messaging;

namespace Core.OS.Persistence;

public sealed class ChangeTrackingInterceptor(IReplicationPublisher publisher, ReplicationSequenceCounter sequenceCounter, TimeProvider timeProvider)
    : ISaveChangesInterceptor
{
    public ChangeTrackingInterceptor(IReplicationPublisher publisher, ReplicationSequenceCounter sequenceCounter)
        : this(publisher, sequenceCounter, TimeProvider.System) { }

    public async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not IModuleDbContext context)
            return result;

        var ignoredEntries = new List<EntityEntry>();
        var entities = context.ChangeTracker.Entries().ToArray();
        GetIgnoredEntities(entities, ignoredEntries, context, false, null);

        var changeList = (from entityEntry in entities
                          let entityType = entityEntry.Entity.GetType()
                          where entityEntry.State is not EntityState.Unchanged and not EntityState.Detached
                          select new ChangedEntity(ignoredEntries.Select(e => e.Entity).Contains(entityEntry.Entity)
                                  ? null
                                  : JsonSerializer.Serialize(entityEntry.Entity, DefaultJsonSerializerSettings.Default),
                              entityType.FullName ?? entityType.Name,
                              entityType.Assembly.FullName,
                              entityEntry.State)).ToList();

        var publishableChanges = changeList.Where(c => c.Entity is not null).ToList();
        if (publishableChanges.Count == 0)
            return result;

        Type? contextType = null;
        foreach (var interfaceType in eventData.Context.GetType().GetInterfaces())
        {
            var innerInterface = interfaceType.GetInterfaces();
            if (innerInterface.Contains(typeof(IModuleDbContext))
                && !innerInterface.Contains(typeof(ISqliteDbContext))
                && !innerInterface.Contains(typeof(IPostgresDbContext)))
            {
                contextType = interfaceType;
                break;
            }
        }

        if (contextType is null)
            return result;

        var contextTypeName = contextType.FullName ?? contextType.Name;
        var changeSet = new DbChangeSet(publishableChanges,
            contextTypeName,
            sequenceCounter.Next(contextTypeName),
            timeProvider.GetUtcNow());

        await publisher.Stage(changeSet, eventData.Context, cancellationToken);

        return result;
    }

    public async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            await publisher.Commit(eventData.Context, cancellationToken);

        return result;
    }

    public async Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            await publisher.Rollback(eventData.Context, cancellationToken);
    }

    public async Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            await publisher.Rollback(eventData.Context, cancellationToken);
    }

    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        => throw new InvalidOperationException("Do not save changes synchronously. Use 'SaveChangesAsync' instead!");

    // Filters out all EntityEntries that are either blacklisted by the context, or relations of other EntityEntries
    private static void GetIgnoredEntities(IEnumerable<EntityEntry> entries, ICollection<EntityEntry> ignoredEntities, IModuleDbContext dbContext, bool isRelation, HashSet<string>? processedNavigations)
    {
        processedNavigations ??= [];

        foreach (var entry in entries)
        {
            // Add blacklisted entities and all relations
            if (isRelation || dbContext.NotSynchronizedEntityTypes.Contains(entry.Entity.GetType()))
                ignoredEntities.Add(entry);

            foreach (var navigation in entry.Navigations)
            {
                if (!processedNavigations.Add($"{entry.Metadata.Name}|{navigation.Metadata.Name}"))
                    continue;

                if (navigation.CurrentValue is IEnumerable navigationCollection)
                {
                    // skip unidirectional relations like tag -> connection
                    if (navigation.Metadata is RuntimeSkipNavigation rt && !rt.IsLeftNavigation())
                        continue;

                    foreach (var relatedEntity in navigationCollection)
                    {
                        var relatedEntityEntry = ((Microsoft.EntityFrameworkCore.DbContext)dbContext).Entry(relatedEntity);
                        GetIgnoredEntities([relatedEntityEntry], ignoredEntities, dbContext, true, processedNavigations);
                    }
                    continue;
                }

                if (navigation.CurrentValue is { } entity)
                {
                    var relatedEntityEntry = ((Microsoft.EntityFrameworkCore.DbContext)dbContext).Entry(entity);
                    GetIgnoredEntities([relatedEntityEntry], ignoredEntities, dbContext, true, processedNavigations);
                }
            }
        }
    }
}
