using Blazor.Shared.Module.Models;
using Core.Shared.Modules.Requests;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Module.Services;

public sealed class ModuleManagementControlPanelState : ControlPanelState
{
    private bool _allowPreReleases;
    private bool _includePreReleases;
    private int _requestErrorCode;
    private string? _requestErrorMessage;

    private readonly List<ModuleState> _moduleStates = [];
    internal List<ModuleMetadataModel> InstalledModules { get; set; } = [];
    internal List<ModuleMetadataModel> AvailableModules { get; set; } = [];

    public bool IncludePreReleases
    {
        get => _includePreReleases;
        set
        {
            if (value != _includePreReleases)
            {
                _includePreReleases = value;

                OnPropertyChanged();
            }
        }
    }

    public bool AllowPreReleases
    {
        get => _allowPreReleases;
        set
        {
            if (value != _allowPreReleases)
            {
                _allowPreReleases = value;

                OnPropertyChanged();
            }
        }
    }

    public int RequestErrorCode
    {
        get => _requestErrorCode;
        set
        {
            if (value != _requestErrorCode)
            {
                _requestErrorCode = value;

                OnPropertyChanged();
            }
        }
    }

    public string? RequestErrorMessage
    {
        get => _requestErrorMessage;
        set
        {
            if (value != _requestErrorMessage)
            {
                _requestErrorMessage = value;

                OnPropertyChanged();
            }
        }
    }

    internal bool IsInitialized { get; private set; }

    internal bool HasChanges { get; private set; }

    internal void Initialize(GetModuleMetadataBundlesResponse response)
    {
        RequestErrorCode = response.RequestError?.ErrorCode ?? 0;
        RequestErrorMessage = response.RequestError?.Message ?? null;
        AllowPreReleases = response.PreReleasesAllowed;

        var models = ModuleMetadataModelFactory.CreateModels(response.Bundles);

        // keep the initial state for change tracking
        _moduleStates.Clear();
        _moduleStates.AddRange(GetStates(models));

        // split them for the tabs
        InstalledModules = [.. models.Where(k => k.Installed).OrderBy(k => k.Title)];
        AvailableModules = [.. models.Where(k => !k.Installed).OrderBy(k => k.Title)];

        IsInitialized = true;
    }

    internal bool EvaluateChanges(List<ModuleMetadataModel> models)
    {
        HasChanges = GetStates(models).Except(_moduleStates).Any();
        return HasChanges;
    }

    private static IEnumerable<ModuleState> GetStates(List<ModuleMetadataModel> models)
        => models.Select(k => new ModuleState(k.Name, k.Version, k.Installed, k.ToBeInstalled, !string.IsNullOrEmpty(k.UpdateVersion)));

    internal async Task CancelEditInOptionGrids()
    {
        foreach (var installedModule in InstalledModules)
        {
            if (installedModule.OptionGrid is not null)
            {
                await installedModule.OptionGrid.CancelEdit();
                installedModule.HasModifiedOptions = false;
            }
        }
    }
}
