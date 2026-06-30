using Blazor.Shared.Profile.Localization;
using Core.Shared.Passkeys;
using Core.Shared.Passkeys.Contracts;
using Core.Shared.Passkeys.Requests;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.FeatureManagement;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using ViciOne.Ui.Blazor.Components.Grid.Services;

namespace Blazor.Shared.Profile.ControlPanels.Passkeys;

public partial class PasskeysControlPanel
{
    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    [Inject(Key = typeof(PasskeyControlPanelServiceKey))]
    public IGridItemSelection<string> SelectedPasskeys { get; set; } = default!;

    private SuiteUser User { get; set; } = null!;
    [Inject] private IControlPanelRequest ControlPanelRequest { get; set; } = default!;

    [Inject]
    private IUiMediator Mediator { get; set; } = default!;

    [Inject]
    private IFeatureManager FeatureManager { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    private IPasskeyHostSupport PasskeyHostSupport { get; set; } = default!;

    public IQueryable<PasskeyInfo> Passkeys { get; private set; } = Enumerable.Empty<PasskeyInfo>().AsQueryable();

    private bool IsSinglePasskeySelected => SelectedPasskeys.Count == 1;
    private bool IsAddPasskeyAllowed { get; set; }

    private bool IsPasskeyHostSupported { get; set; }

    private bool ShowUnavailableNotice => !IsAddPasskeyAllowed && !Passkeys.Any();

    private string? AddPasskeyTitle => IsPasskeyHostSupported ? null : Passkey.RequiresDnsHost;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        IsAddPasskeyAllowed = await FeatureManager.IsEnabledAsync(Core.Shared.Features.Constants.PasskeyFeatureName);
        IsPasskeyHostSupported = PasskeyHostSupport.IsPasskeyCapableHost(new Uri(NavigationManager.BaseUri).Host);

        SelectedPasskeys.Clear();

        var user = await GetAuthenticatedUser();
        if (user is null)
            return;

        User = user;
        State.User = user;

        await InitializePasskeys();

        State.Changed += StateChanged;
        SelectedPasskeys.Changed += SelectionChanged;
    }

    protected override ValueTask DisposeAsyncCore()
    {
        State.Changed -= StateChanged;
        SelectedPasskeys.Changed -= SelectionChanged;

        return base.DisposeAsyncCore();
    }

    private async void SelectionChanged(GridItemSelectionChangedEventArgs<string> args)
        => await InvokeAsync(StateHasChanged);

    private async void StateChanged(ControlPanelStateChangedEventArgs obj)
    {
        if (!obj.PropertyNames.Contains(nameof(PasskeysControlPanelState.PasskeysMarkedForDeletion)))
            return;

        SelectedPasskeys.Clear();
        await InitializePasskeys();
        await InvokeAsync(StateHasChanged);
    }

    private async Task<SuiteUser?> GetAuthenticatedUser()
    {
        var authenticationState = AuthState is null ? null : await AuthState;
        if (authenticationState is null)
            return null;

        return await UserManager.GetUserAsync(authenticationState.User);
    }

    private async Task InitializePasskeys()
    {
        var response = await Mediator.Request<GetPasskeys, GetPasskeysResponse>(new GetPasskeys(User.Id));
        Passkeys = response.Passkeys.AsQueryable();
    }

    private async Task ShowAddPasskey()
        => await ControlPanelRequest.Send<AddPasskeyControlPanel, AddPasskeysControlPanelState>(s =>
        {
            s.ExistingUserPasskeys = Passkeys.ToArray();
        });

    private async Task ShowRenamePasskey()
    {
        var selectedId = SelectedPasskeys.Single();
        var passkey = Passkeys.First(info => info.Id == selectedId);

        await ControlPanelRequest.Send<RenamePasskeyControlPanel, RenamePasskeyControlPanelState>(s =>
        {
            s.UserId = User.Id;
            s.PasskeyId = selectedId;
            s.CurrentName = passkey.Name;
            s.NewName = passkey.Name;
            s.ExistingNames = Passkeys.Select(info => info.Name).OfType<string>().ToArray();
        });

        await InitializePasskeys();
        await InvokeAsync(StateHasChanged);
    }

    private async Task MarkPasskeysForDeletion()
    {
        SelectedPasskeys.BeginUpdate();
        try
        {
            var ids = SelectedPasskeys.Select(s => s).ToArray();
            State.PasskeysMarkedForDeletion.AddRange(ids);
            await BeginEdit();
        }
        finally
        {
            SelectedPasskeys.EndUpdate();
        }
    }
}
