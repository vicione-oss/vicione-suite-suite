using System.Diagnostics.CodeAnalysis;
using Blazor.Shared.Settings.Factories;
using Blazor.Shared.Settings.Services;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Settings.Models;

/// <summary>
/// Owns the control panel edits of one visit to a top-level control panel, one edit per control panel, so the edits of a
/// parent and its children run side by side and survive tab switches.
/// </summary>
internal sealed class ControlPanelEditScope(IControlPanelEditRegistry controlPanelEditRegistry,
    ControlPanelEditFactory controlPanelEditFactory) : IAsyncDisposable
{
    private readonly Lock _lock = new();
    private readonly Dictionary<IControlPanelRegistryItem, IControlPanelEdit> _edits = new(ReferenceEqualityComparer.Instance);

    // The edits whose reset is done for this visit.
    private readonly HashSet<IControlPanelEdit> _resetEdits = new(ReferenceEqualityComparer.Instance);

    // The groups of edits sharing a reset handler whose reset is done for this visit.
    private readonly HashSet<SharedResetGroup> _resetSharedResetGroups = [];

    private bool _disposed;

    /// <returns>
    /// <see langword="false"/> when the visit has ended, e.g. a container is still being initialized while the carousel
    /// already disposed its scope. Not thrown, as this is called from <c>OnInitialized</c> of components.
    /// </returns>
    public bool TryGetOrCreateEdit(IControlPanelRegistryItem controlPanelRegistryItem,
        [NotNullWhen(true)] out IControlPanelEdit? controlPanelEdit)
    {
        lock (_lock)
        {
            if (_disposed)
            {
                controlPanelEdit = null;
                return false;
            }

            if (_edits.TryGetValue(controlPanelRegistryItem, out controlPanelEdit))
                return true;

            controlPanelEdit = controlPanelEditFactory.CreateControlPanelEdit(controlPanelRegistryItem);
            _edits.Add(controlPanelRegistryItem, controlPanelEdit);

            // Registered under the lock so a concurrent DisposeAsync cannot remove the scope's edits before this one is added.
            controlPanelEditRegistry.Add(controlPanelEdit);

            return true;
        }
    }

    /// <returns>
    /// <see langword="true"/> the first time per visit, when the reset of <paramref name="controlPanelEdit"/> is due: once per
    /// edit, or once for all edits sharing a reset handler, so mounting a tab does not discard what was loaded and changed before.
    /// </returns>
    public bool TryMarkReset(IControlPanelEdit controlPanelEdit)
    {
        lock (_lock)
            return controlPanelEdit.SharedResetGroup is { } sharedResetGroup
                ? _resetSharedResetGroups.Add(sharedResetGroup)
                : _resetEdits.Add(controlPanelEdit);
    }

    /// <summary>
    /// Lets every control panel of this scope be reset again when it is next mounted, e.g. after navigating back to a
    /// parent that was unmounted together with its children.
    /// </summary>
    public void ClearResets()
    {
        lock (_lock)
        {
            _resetEdits.Clear();
            _resetSharedResetGroups.Clear();
        }
    }

    public async ValueTask DisposeAsync()
    {
        IControlPanelEdit[] controlPanelEdits;

        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;

            controlPanelEdits = [.. _edits.Values];
            _edits.Clear();
            _resetEdits.Clear();
            _resetSharedResetGroups.Clear();
        }

        controlPanelEditRegistry.Remove(e => controlPanelEdits.Contains(e));

        foreach (var controlPanelEdit in controlPanelEdits)
            await controlPanelEdit.DisposeAsync();
    }
}
