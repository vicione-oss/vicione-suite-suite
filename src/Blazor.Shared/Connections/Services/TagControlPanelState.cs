using Blazor.Shared.Connections.Contracts;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Connections.Services;

public sealed class TagControlPanelState : ControlPanelState
{
    private Guid? _tagId;

    /// <remarks>
    /// <see cref="TagControlPanel"/> will use this property to decide whether a tag should be edited (value is set) or created (value is null)
    /// </remarks>
    public Guid? TagId
    {
        get => _tagId;
        set
        {
            if (value != _tagId)
            {
                _tagId = value;

                OnPropertyChanged();
            }
        }
    }

    internal EditTagModel? Tag { get; set; }
    internal bool IsNew { get; set; }
}
