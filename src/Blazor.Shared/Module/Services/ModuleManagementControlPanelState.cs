using Blazor.Shared.Module.Models;
using Core.Shared.Modules.Requests;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Module.Services;

public sealed class ModuleManagementControlPanelState : ControlPanelState
{
    private bool _allowPreReleases;
    private int _requestErrorCode;
    private string? _requestErrorMessage;
    private List<ModuleMetadataModel> _installedModules = [];
    private List<ModuleMetadataModel> _availableModules = [];

    internal List<ModuleMetadataModel> InstalledModules
    {
        get => _installedModules;
        set
        {
            if (value == _installedModules)
                return;

            _installedModules = value;

            OnPropertyChanged(nameof(InstalledModules));
        }
    }

    internal List<ModuleMetadataModel> AvailableModules
    {
        get => _availableModules;
        set
        {
            if (value == _availableModules)
                return;

            _availableModules = value;

            OnPropertyChanged(nameof(AvailableModules));
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

    internal void Initialize(GetModuleMetadataBundlesResponse response)
    {
        RequestErrorCode = response.RequestError?.ErrorCode ?? 0;
        RequestErrorMessage = response.RequestError?.Message ?? null;

        var models = ModuleMetadataModelFactory.CreateModels(response.Bundles);

        // split them for the tabs
        InstalledModules = [.. models.Where(k => k.Installed).OrderBy(k => k.Title)];
        AvailableModules = [.. models.Where(k => !k.Installed).OrderBy(k => k.Title)];

        IsInitialized = true;
    }

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
