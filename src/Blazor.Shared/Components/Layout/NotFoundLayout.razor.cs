using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace Blazor.Shared.Components.Layout;

public sealed partial class NotFoundLayout : LayoutComponentBase
{
    [Inject] private IStringLocalizer<NotFoundLayout> Loc { get; set; } = default!;
}
