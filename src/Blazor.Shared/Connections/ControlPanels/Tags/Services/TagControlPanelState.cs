using Blazor.Shared.Connections.Contracts;
using Sdk.Client.ControlPanels.Services;
using ViciOne.SourceGenerators.NotifyChanged;

namespace Blazor.Shared.Connections.ControlPanels.Tags.Services;

public sealed partial class TagControlPanelState : ControlPanelState
{
    /// <remarks>
    /// <see cref="TagControlPanel"/> will use this property to decide whether a tag should be edited (value is set) or created (value is null)
    /// </remarks>
    [NotifyChanged]
    public partial Guid? TagId { get; set; }

    internal EditTagModel? Tag { get; set; }
    internal bool IsEditMode => TagId.HasValue;
}
