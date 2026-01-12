using System.Collections;
using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Sdk.Backend.Messaging;
using Sdk.Backend.Persistence;
using Sdk.Messaging;

namespace Core.OS.Persistence;

public sealed class ChangeTrackingInterceptor(ISuiteMediator mediator) : ISaveChangesInterceptor
{
    private readonly ISuiteMediator _mediator = mediator;
    private readonly ConcurrentQueue<List<ChangedEntity>> _stagedChanges = new();

    public ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not IModuleDbContext context)
            return new ValueTask<InterceptionResult<int>>(result);

        var ignoredEntries = new List<EntityEntry>();
        var entities = context.Instance.ChangeTracker.Entries().ToArray();
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

        _stagedChanges.Enqueue(changeList);

        return new ValueTask<InterceptionResult<int>>(result);
    }

    public async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (result <= 0)
            return result;

        if (eventData.Context is not IModuleDbContext)
            return result;

        if (!_stagedChanges.TryDequeue(out var changes))
            throw new DbUpdateException("Db was changed, but no changes were staged for distribution");

        if (changes.Count != result)
            throw new DbUpdateException(
                $"{result} changes were saved, but {changes.Count} changes were staged for distribution");

        Type? contextType = null;
        foreach (var interfaceType in eventData.Context.GetType().GetInterfaces())
        {
            var innerInterface = interfaceType.GetInterfaces();
            if (innerInterface.Contains(typeof(IModuleDbContext))
                && !innerInterface.Contains(typeof(ISqliteDbContext)) // for convenience
                && !innerInterface.Contains(typeof(IPostgresDbContext)))
            {
                contextType = interfaceType;
                break;
            }
        }

        if (contextType is null || !changes.Any(c => c.Entity is not null))
            return result;

        var message = new DbChangeSet(changes.Where(c => c.Entity is not null).ToList(), contextType.FullName ?? contextType.Name);
        await _mediator.Publish(message, cancellationToken);

        return result;
    }

    public Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _stagedChanges.TryDequeue(out _);
        return Task.CompletedTask;
    }

    public Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
    {
        _stagedChanges.TryDequeue(out _);
        return Task.CompletedTask;
    }

    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        throw new InvalidOperationException("Do not save changes synchronously. Use 'SaveChangesAsync' instead!");
    }

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
                        var relatedEntityEntry = dbContext.Instance.Entry(relatedEntity);
                        GetIgnoredEntities([relatedEntityEntry], ignoredEntities, dbContext, true, processedNavigations);
                    }
                    continue;
                }

                if (navigation.CurrentValue is { } entity)
                {
                    var relatedEntityEntry = dbContext.Instance.Entry(entity);
                    GetIgnoredEntities([relatedEntityEntry], ignoredEntities, dbContext, true, processedNavigations);
                }
            }
        }
    }
}
