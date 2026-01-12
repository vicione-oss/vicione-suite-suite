using Blazor.Shared.Connections.Services;
using Blazor.Shared.Services;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;

namespace Blazor.Shared.Connections.ControlPanels;

[ControlPanelCategory<ControlPanelSystemCategoryDescriptor>]
public sealed partial class TagControlPanel : ControlPanelBase<TagControlPanelState>;
