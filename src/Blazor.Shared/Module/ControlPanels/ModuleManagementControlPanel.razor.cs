using Blazor.Shared.Module.Models;
using Blazor.Shared.Module.Services;
using Blazor.Shared.Services;
using Core.Shared.Modules.Events;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.Infrastructure;
using Sdk.Client.Services;
using Sdk.Modules;
using Sdk.Utils;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Module.ControlPanels;

/// <summary>
/// todo - changing the available version will can lead to changed dependencies, options etc
/// where to store the options later if we enter passwords etc.
/// </summary>

[ControlPanelCategory<ControlPanelSystemCategoryDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class ModuleManagementControlPanel : ControlPanelBase<ModuleManagementControlPanelState>,
    IEventConsumer<ModuleOptionsUpdatedEvent>
{
    private readonly AutoDisposeList<IDisposable> _subscriptionHandle = [];

    [Inject]
    internal IModuleManagementService ManagementService { get; set; } = default!;

    [Inject]
    internal IUiMediator Mediator { get; set; } = default!;

    [Inject]
    public IMessageBannerService BannerService { get; set; } = default!;

    [Inject]
    public ISuiteControlService SuiteControlService { get; set; } = default!;

    [Inject]
    public ILogger<ModuleManagementControlPanel> Logger { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        _subscriptionHandle.Add(Mediator.Register(this));
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _subscriptionHandle.Dispose();

        await base.DisposeAsyncCore();
    }

    private async Task IncludePreReleaseStateChanged(bool change)
    {
        if (State.IncludePreReleases == change)
            return;

        State.IncludePreReleases = change;

        await LoadModuleVersions(true);

        await InvokeAsync(StateHasChanged);
    }

    private Task BeginEditIfChanged()
    {
        if (State.EvaluateChanges([.. State.InstalledModules, .. State.AvailableModules]))
        {
            return BeginEdit();
        }

        // we have no changes here -> State.HasChanges == false
        return CancelEdit();
    }

    private async Task LoadModuleVersions(bool forceReload = false)
    {
        State.BeginLoading();
        try
        {
            // load metadata assets and jsons in one step
            var response = await ManagementService.GetModuleMetadata(State.IncludePreReleases, forceReload);

            State.Initialize(response);
        }
        finally
        {
            State.EndLoading();
        }
    }

    private void SetVersionUpdate(ModuleMetadataModel module)
    {
        if (module.UpdateVersion == module.SelectedVersion)
            return;

        module.UpdateVersion = module.SelectedVersion;
        BeginEditIfChanged();
    }

    private void ResetVersionUpdate(ModuleMetadataModel module)
    {
        module.UpdateVersion = null;
        BeginEditIfChanged();
    }

    private async Task AddModuleOption(ModuleMetadataModel module, ModuleOptionType optionType)
    {
        ModuleOptionDeclaration? customOption = null;

        var key = GetUniqueModuleOptionKey(module.EditOptions);

        if (optionType == ModuleOptionType.Boolean)
            customOption = new ModuleOptionDeclaration { Key = key, Value = $"{false}", OptionType = ModuleOptionType.Boolean };
        else if (optionType == ModuleOptionType.Number)
            customOption = new ModuleOptionDeclaration { Key = key, Value = "1", OptionType = ModuleOptionType.Number };
        else if (optionType == ModuleOptionType.Text)
            customOption = new ModuleOptionDeclaration { Key = key, Value = CommonVocabulary.Value, OptionType = ModuleOptionType.Text };

        if (customOption is not null)
        {
            module.EditOptions.Add(customOption.Key, customOption);
            module.CustomOptions.Add(customOption);

            await BeginEdit();
        }
    }

    private static string GetUniqueModuleOptionKey(Dictionary<string, ModuleOptionDeclaration> moduleOptionMap)
    {
        string key;
        var keyCheckLoopCount = 0;
        do
        {
            if (keyCheckLoopCount++ > 0)
                key = $"{CommonVocabulary.Key} {keyCheckLoopCount}";
            else
                key = $"{CommonVocabulary.Key}";
        }
        while (moduleOptionMap.ContainsKey(key));

        return key;
    }

    private static bool ValidateModuleOptionKey(ModuleMetadataModel module, string key, ModuleOptionDeclaration moduleOption)
    {
        if (module.EditOptions.TryGetValue(key, out var mappedModuleOption))
            return mappedModuleOption == moduleOption;
        else
            return true;
    }

    private static bool CanDeleteModuleOption(ModuleMetadataModel module, ModuleOptionDeclaration moduleOption)
        => module.CustomOptions.Contains(moduleOption);

    private static Task ModuleOptionChanged(ModuleMetadataModel module, ModuleOptionDeclaration moduleOption)
    {
        var p = module.EditOptions.First(p => p.Value == moduleOption);
        if (p.Key != moduleOption.Key)
        {
            module.EditOptions.Remove(p.Key);
            module.EditOptions.Add(moduleOption.Key, moduleOption);
        }

        module.HasModifiedOptions = true;
        //await BeginEdit();    // when we save all at once?
        return Task.CompletedTask;
    }

    private static Task ModuleOptionsRemoved(ModuleMetadataModel module, IEnumerable<ModuleOptionDeclaration> moduleOptions)
    {
        foreach (var moduleOption in moduleOptions)
        {
            if (module.EditOptions.Remove(moduleOption.Key))
            {
                module.CustomOptions.Remove(moduleOption);
                module.HasModifiedOptions = true;
                //await BeginEdit(); // when we save all at once?
            }
        }
        return Task.CompletedTask;
    }

    private static async Task ModuleOptionsExpandedChanged(ModuleMetadataModel module)
    {
        if (!module.OptionsExpanded && module.OptionGrid is not null)
        {
            await module.OptionGrid.CancelEdit();
            module.HasModifiedOptions = false;
        }
    }

    private async Task InstalledModuleInstalledFlagChanged(ModuleMetadataModel module)
    {
        if (!module.Installed && module.OptionGrid is not null)
        {
            await module.OptionGrid.CancelEdit();
            module.HasModifiedOptions = false;
        }

        await BeginEditIfChanged();
    }

    private async Task UpdateModuleOptions(ModuleMetadataModel module, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(module.ModuleId))
            return;

        await ManagementService.UpdateModuleOptions(module.ModuleId, module.EditOptions.Values, cancellationToken);

        module.HasModifiedOptions = false;
    }

    public Task Consume(ClientContext<ModuleOptionsUpdatedEvent> context, CancellationToken cancellationToken)
    {
        var module = State.InstalledModules.FirstOrDefault(k => k.ModuleId == context.Message.ModuleId);
        if (module != null)
        {
            module.HasModifiedOptions = false;
        }

        return Task.CompletedTask;
    }
}
