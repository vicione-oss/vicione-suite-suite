using System.Globalization;
using Blazor.Server.Backend.Localization;
using Core.Shared.Security;
using Core.Shared.UserManagement.Configuration;
using Microsoft.AspNetCore.Components;

namespace Blazor.Server.Backend.Components;

public sealed partial class AddExternalLoginContent
{
    private const string CancelFormId = "cancel-add-external-login";
    private const string LinkFormId = "add-external-login";

    private bool _isExternalIdProviderConfigured;

    [Parameter]
    public bool BuildingLayout { get; set; }

    [Inject]
    private IExternalAuthenticationSettings ExternalAuthenticationSettings { get; set; } = default!;

    private static string Description
        => string.Format(CultureInfo.CurrentCulture, AddExternalLogin.Description, ProviderConstants.DefaultProviderDisplayName);

    private static string LinkButtonText
        => string.Format(CultureInfo.CurrentCulture, AddExternalLogin.LinkWithProvider, ProviderConstants.DefaultProviderDisplayName);

    protected override async Task OnInitializedAsync()
        => _isExternalIdProviderConfigured
            = await ExternalAuthenticationSettings.IsExternalAuthenticationProviderConfigured();
}
