using Blazor.Shared.Module.ControlPanels.Services;
using Blazor.Shared.Module.Models;
using Blazor.Shared.Module.Services;
using Blazor.Shared.Services;
using Core.Shared.Modules.Events;
using Microsoft.AspNetCore.Components;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.Infrastructure;
using Sdk.Client.Services;
using Sdk.MessageBanner.Contracts;
using Sdk.Messaging;
using Sdk.Modules;
using Sdk.Utils;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Module.ControlPanels;

[ControlPanelCategory<ControlPanelSystemCategoryDescriptor>]
public partial class ModuleDetailsControlPanel : ControlPanelBase<ModuleDetailsControlPanelState>,
    IEventConsumer<ModuleOptionsUpdatedEvent>,
    IEventConsumer<ModulePackageOperationsChanged>,
    IEventConsumer<ModulePackageOperationsFailed>
{
    private bool _isDetailsExpanded = true;
    private bool _isDebug;
    private readonly AutoDisposeList<IDisposable> _subscriptionHandles = [];

    [Inject] internal IModuleManagementService ManagementService { get; set; } = default!;
    [Inject] private IMessageBannerService MessageBannerService { get; set; } = default!;
    [Inject] private IUiMediator Mediator { get; set; } = default!;

    protected override void OnInitialized()
    {
        base.OnInitialized();

#if DEBUG
        _isDebug = true;
#endif

        _subscriptionHandles.Add(Mediator.Register<ModuleOptionsUpdatedEvent>(this));
        _subscriptionHandles.Add(Mediator.Register<ModulePackageOperationsChanged>(this));
        _subscriptionHandles.Add(Mediator.Register<ModulePackageOperationsFailed>(this));
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

    public Task Consume(ClientContext<ModuleOptionsUpdatedEvent> context, CancellationToken cancellationToken)
    {
        if (State.ModuleMetadata?.ModuleId != context.Message.ModuleId)
            return Task.CompletedTask;

        State.ModuleMetadata?.HasModifiedOptions = false;

        return Task.CompletedTask;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _subscriptionHandles.Dispose();

        await base.DisposeAsyncCore();
    }

    public async Task Consume(ClientContext<ModulePackageOperationsChanged> context, CancellationToken cancellationToken)
    {
        if (State.ModuleMetadata is null)
            return;

        var change = context.Message.Changes.FirstOrDefault(o => o.Operation.Package.Name == State.ModuleMetadata.Name);
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

    public Task Consume(ClientContext<ModulePackageOperationsFailed> context, CancellationToken cancellationToken)
    {
        MessageBannerService.ShowMessageBanner(MessageType.Error, context.Message.Error.Message);

        return Task.CompletedTask;
    }
}
