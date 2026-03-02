using System.Collections;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Sdk.Authorization;
using Sdk.Authorization.Extensions;
using Sdk.Client.Modules;
using Sdk.Client.NavTiles.Attributes;
using Sdk.Client.NavTiles.Components;
using Sdk.Client.NavTiles.Enums;
using Sdk.Client.NavTiles.Services;

namespace Blazor.Shared.NavTiles.Services;

internal sealed class NavTileRegistry<TClientModule> : INavTileRegistry<TClientModule>
    where TClientModule : class, IClientModule
{
    private readonly Lock _concurrentLock = new();
    private readonly Dictionary<string, NavTileRegistryItem> _itemMap = [];
    private bool _hasDeferredChanges;

    public int UpdateLock { get; private set; }

    public event Action? Changed;

    public NavTileRegistry()
    {
        var types = typeof(TClientModule).Assembly.GetExportedTypes()
            .Where(t => Attribute.IsDefined(t, typeof(InitialNavTileAttribute<TClientModule>)))
            .Where(t => typeof(INavTile).IsAssignableFrom(t))
            .Select(t => new
            {
                ComponentType = t,
                InitialNavTileAttribute = t.GetCustomAttribute<InitialNavTileAttribute<TClientModule>>()!,
                ModuleAuthorizeAttribute = t.GetCustomAttribute<ModuleAuthorizeAttribute>(),
            });

        foreach (var r in types)
        {
            var item = new NavTileRegistryItem
            {
                Id = r.InitialNavTileAttribute.Id,
                ComponentType = r.ComponentType,
                Group = r.InitialNavTileAttribute.Group,
                State = new NavTileState
                {
                    HorizontalSpan = r.InitialNavTileAttribute.HorizontalSpan,
                    Enabled = r.InitialNavTileAttribute.Enabled,
                    LinkTarget = r.InitialNavTileAttribute.LinkTarget
                },
                AuthorizationRequirement = r.ModuleAuthorizeAttribute.GetAccessLevelRequirement()
            };

            _itemMap.Add(item.Id, item);
        }
    }

    public INavTileRegistryItem Add<T>(string id, NavTileSpan horizontalSpan = NavTileSpan.One, bool enabled = true,
        string? linkTarget = null, NavTileGroup group = NavTileGroup.Applications, IAuthorizationRequirement? authorizationRequirement = null)
            where T : ComponentBase, INavTile
    {
        var item = new NavTileRegistryItem
        {
            Id = id,
            ComponentType = typeof(T),
            State = new NavTileState { HorizontalSpan = horizontalSpan, Enabled = enabled, LinkTarget = linkTarget },
            Group = group,
            AuthorizationRequirement = authorizationRequirement
        };

        lock (_concurrentLock)
        {
            _itemMap.Add(item.Id, item);

            if (UpdateLock == 0)
                Changed?.Invoke();
            else
                _hasDeferredChanges = true;
        }

        return item;
    }

    public bool Remove(string id)
    {
        lock (_concurrentLock)
        {
            if (_itemMap.Remove(id))
            {
                if (UpdateLock == 0)
                    Changed?.Invoke();
                else
                    _hasDeferredChanges = true;

                return true;
            }
            else
            {
                return false;
            }
        }
    }

    public IEnumerator<INavTileRegistryItem> GetEnumerator()
    {
        lock (_concurrentLock)
        {
            return _itemMap.Values.ToList().GetEnumerator();
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        lock (_concurrentLock)
        {
            return _itemMap.Values.ToList().GetEnumerator();
        }
    }

    public void BeginUpdate()
    {
        lock (_concurrentLock)
        {
            UpdateLock++;
        }
    }

    public void EndUpdate()
    {
        lock (_concurrentLock)
        {
            UpdateLock--;

            if (UpdateLock <= 0)
            {
                UpdateLock = 0;

                if (_hasDeferredChanges)
                {
                    Changed?.Invoke();

                    _hasDeferredChanges = false;
                }
            }
        }
    }
}
