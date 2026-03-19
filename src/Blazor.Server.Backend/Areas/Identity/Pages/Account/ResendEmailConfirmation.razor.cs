using Core.Shared.Instance.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Account;

[AllowAnonymous]
public sealed partial class ResendEmailConfirmation
{
    [Inject]
    private ILoginDesignService LoginDesignService { get; set; } = default!;
}
