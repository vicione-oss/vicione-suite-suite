using System.Reflection;
using Blazor.Shared.Module.Services;
using Blazor.Shared.Services;
using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Components;

public sealed partial class Routes
{
    private readonly List<string> _moduleStylesheets = [];
    private readonly List<Assembly> _additionalAssemblies = [];

    private bool _initialized;
    [Inject] private IClientModuleService ModuleService { get; set; } = default!;
    [Inject] private IServiceProvider ServiceProvider { get; set; } = default!;
    [Inject] private INavigationService NavigationService { get; set; } = default!;

    protected override void OnInitialized()
    {
        _additionalAssemblies.AddRange(ModuleService.GetModuleAssemblies());
        _moduleStylesheets.AddRange(ModuleService.GetAllModuleStylesheets());
    }

    protected override async Task OnInitializedAsync()
    {
        // The service provider is injected scoped, so the shared scope is initialized here.
        await ModuleService.InitializeServices(ServiceProvider);

        _initialized = true;
    }
}
