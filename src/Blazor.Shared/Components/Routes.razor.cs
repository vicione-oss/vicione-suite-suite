using System.Reflection;
using Blazor.Shared.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Client.Services;

namespace Blazor.Shared.Components;

public sealed partial class Routes
{
    private readonly List<string> _moduleStylesheets = [];
    private readonly List<Assembly> _additionalAssemblies = [];

    private bool _initialized;
#pragma warning disable CS0618 // Type or member is obsolete
    [Inject] private IClientModuleService ModuleService { get; set; } = default!;
#pragma warning restore CS0618
    [Inject] private IServiceProvider ServiceProvider { get; set; } = default!;
    [Inject] private INavigationService NavigationService { get; set; } = default!;

    protected override void OnInitialized()
    {
        _additionalAssemblies.AddRange(ModuleService.GetModuleAssemblies());
        _moduleStylesheets.AddRange(ModuleService.GetAllModuleStylesheets());
    }

    protected override async Task OnInitializedAsync()
    {
        // here the service provider is injected scoped so we initialize our shared scope
        await ModuleService.InitializeServices(ServiceProvider);

        _initialized = true;
    }
}
