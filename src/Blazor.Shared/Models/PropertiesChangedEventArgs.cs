using Blazor.Shared.Interfaces;

namespace Blazor.Shared.Models;

/// <summary>
/// Arguments for <see cref="IHasChangeableProperties.Changed"/> event
/// </summary>
public sealed class PropertiesChangedEventArgs(IHasChangeableProperties sender, IReadOnlySet<string> propertyNames) : EventArgs
{
    /// <summary>
    /// Instance that raised the associated <see cref="IHasChangeableProperties.Changed"/> event
    /// </summary>
    public IHasChangeableProperties Sender => sender;

    /// <summary>
    /// Name of the properties that have been changed
    /// </summary>
    public IReadOnlySet<string> PropertyNames => propertyNames;
}
