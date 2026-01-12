using Microsoft.AspNetCore.Authorization;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Modules;

namespace Blazor.Shared.Settings.Models;

internal sealed record ControlPanelRegistryItem<TClientModule>(Type ComponentType, IControlPanelDescriptor Descriptor,
    IControlPanelState State, IControlPanelCategoryDescriptor CategoryDescriptor, IControlPanelGroupDescriptor GroupDescriptor,
    IAuthorizationRequirement? AuthorizationRequirement) :
        IControlPanelRegistryItem<TClientModule>
            where TClientModule : class, IClientModule;
