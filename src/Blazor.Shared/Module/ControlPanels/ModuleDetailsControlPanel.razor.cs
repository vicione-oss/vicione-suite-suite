using Blazor.Shared.Module.ControlPanels.Services;
using Blazor.Shared.Module.Models;
using Blazor.Shared.Module.Services;
using Blazor.Shared.Services;
using Core.Shared.Modules.Events;
using Microsoft.AspNetCore.Components;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.Services;
using Sdk.MessageBanner.Contracts;
using Sdk.Messaging;
using Sdk.Modules;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Module.ControlPanels;

[ControlPanelCategory<ControlPanelSystemCategoryDescriptor>]
public partial class ModuleDetailsControlPanel : ControlPanelBase<ModuleDetailsControlPanelState>
{
#if DEBUG
    private const bool IsDebug = true;
#else
    private const bool IsDebug = false;
#endif

    [Inject] internal IModuleManagementService ManagementService { get; set; } = default!;
    [Inject] private IMessageBannerService MessageBannerService { get; set; } = default!;

    protected override async ValueTask DisposeAsyncCore()
    {
        ManagementService.OptionsChanged -= ManagementServiceOptionsChanged;
        ManagementService.OperationsChanged -= ManagementServiceOperationsChanged;

        await base.DisposeAsyncCore();
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        ManagementService.OptionsChanged += ManagementServiceOptionsChanged;
        ManagementService.OperationsChanged += ManagementServiceOperationsChanged;
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

    private async Task ModuleOptionChanged(ModuleMetadataModel module, ModuleOptionDeclaration moduleOption)
    {
        var p = module.EditOptions.First(p => p.Value == moduleOption);
        if (p.Key != moduleOption.Key)
        {
            module.EditOptions.Remove(p.Key);
            module.EditOptions.Add(moduleOption.Key, moduleOption);
        }

        module.HasModifiedOptions = true;
        await BeginEdit();
    }

    private async Task ModuleOptionsRemoved(ModuleMetadataModel module, IEnumerable<ModuleOptionDeclaration> moduleOptions)
    {
        foreach (var moduleOption in moduleOptions)
        {
            if (module.EditOptions.Remove(moduleOption.Key))
            {
                module.CustomOptions.Remove(moduleOption);
                module.HasModifiedOptions = true;
                await BeginEdit();
            }
        }
    }

    private static async Task ModuleOptionsExpandedChanged(ModuleMetadataModel module)
    {
        if (!module.OptionsExpanded && module.OptionGrid is not null)
        {
            await module.OptionGrid.CancelEdit();
            module.HasModifiedOptions = false;
        }
    }

    private async Task AfterVersionToInstallSelect()
    {
        if (State.ModuleMetadata is not null &&
            State.ModuleMetadata.Installed &&
            State.VersionToInstall == State.ModuleMetadata.Version)
        {
            return;
        }

        await BeginEdit();
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

    private async Task ManagementServiceOperationsChanged(ModulePackageOperationsChanged changeEvent, CancellationToken token)
    {
        if (changeEvent.Error is not null)
        {
            MessageBannerService.ShowMessageBanner(MessageType.Error, changeEvent.Error.Message);
            return;
        }

        if (State.ModuleMetadata is null)
            return;

        var change = changeEvent.Changes.FirstOrDefault(o => o.Operation.Package.Name == State.ModuleMetadata.Name);
        if (change is null)
            return;

        if (change.Action is CrudAction.Deleted)
        {
            State.ModuleMetadata.PendingOperation = null;
            State.VersionToInstall = string.Empty;
            await InvokeAsync(StateHasChanged);
            return;
        }

        State.ModuleMetadata.PendingOperation = change.Operation;

        await InvokeAsync(StateHasChanged);
    }

    private Task ManagementServiceOptionsChanged(ModuleOptionsChanged changeEvent, CancellationToken token)
    {
        if (State.ModuleMetadata?.ModuleId != changeEvent.ModuleId)
            return Task.CompletedTask;

        State.ModuleMetadata?.HasModifiedOptions = false;

        return Task.CompletedTask;
    }
}
