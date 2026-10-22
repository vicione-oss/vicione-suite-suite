using Core.Shared.Instance.Services;
using Microsoft.AspNetCore.Components;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Account;

public sealed partial class AddExternalLogin
{
    [Inject]
    private ILoginDesignService LoginDesignService { get; set; } = default!;
}
