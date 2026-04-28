using Blazor.Shared.Settings.NetworkInterface.Enums;
using Blazor.Shared.Settings.NetworkInterface.Enums.Extensions;
using Microsoft.AspNetCore.Components;
using ViciOne.Ui.Blazor.Components.ComboBox;

namespace Blazor.Shared.Settings.NetworkInterface.Components;

public sealed partial class IpV4SettingsGroup : ComponentBase
{
    private bool _dialogVisible;
    private bool _confirmed;
    private IpConfigurationMode _configurationModeBefore;

    private static List<ComboBoxItem<IpConfigurationMode, string>> ConfigurationModeComboBoxItems => [..
        Enum.GetValues<IpConfigurationMode>()
            .Where(mode => mode != IpConfigurationMode.LinkLocal)
            .Select(ipConfigurationMode => new ComboBoxItem<IpConfigurationMode, string>
            {
                Value = ipConfigurationMode,
                Text = ipConfigurationMode.ToLocalizedString()
            })
            .OrderBy(i => i.Text)];

    [Parameter, EditorRequired] public string Title { get; set; }
    [Parameter] public IpConfigurationMode ConfigurationMode { get; set; }
    [Parameter] public EventCallback<IpConfigurationMode> ConfigurationModeChanged { get; set; }
    [Parameter, EditorRequired] public string IpAddress { get; set; }
    [Parameter] public EventCallback<string> IpAddressChanged { get; set; }
    [Parameter, EditorRequired] public string SubnetMask { get; set; }
    [Parameter] public EventCallback<string> SubnetMaskChanged { get; set; }
    [Parameter] public string? DefaultGateway { get; set; }
    [Parameter] public EventCallback<string?> DefaultGatewayChanged { get; set; }
    [Parameter] public RenderFragment? AdditionalSettingsFields { get; set; }
    [Parameter] public EventCallback Changed { get; set; }
    [Parameter, EditorRequired] public bool HasManualConfiguredDnsServers { get; set; }


    private IpConfigurationMode ConfigurationModeWrapper
    {
        // not allowed to select LinkLocal, because it is only used internally when DHCP is enabled but no lease is available
        get => ConfigurationMode == IpConfigurationMode.LinkLocal ? IpConfigurationMode.AutomaticDhcp : ConfigurationMode;
        set => ConfigurationMode = value;
    }

    protected override void OnInitialized()
        => _configurationModeBefore = ConfigurationMode;

    private async Task NotifyConfigurationModeChanged()
    {
        if (ConfigurationModeChanged.HasDelegate)
            await ConfigurationModeChanged.InvokeAsync(ConfigurationMode);

        await NotifyChanged();
    }

    private async Task NotifyIpAddressChanged()
    {
        if (IpAddressChanged.HasDelegate)
            await IpAddressChanged.InvokeAsync(IpAddress);

        await NotifyChanged();
    }

    private async Task NotifySubnetMaskChanged()
    {
        if (SubnetMaskChanged.HasDelegate)
            await SubnetMaskChanged.InvokeAsync(SubnetMask);

        await NotifyChanged();
    }

    private async Task NotifyDefaultGatewayChanged()
    {
        if (DefaultGatewayChanged.HasDelegate)
            await DefaultGatewayChanged.InvokeAsync(DefaultGateway);

        await NotifyChanged();
    }

    private async Task NotifyChanged()
    {
        if (Changed.HasDelegate)
            await Changed.InvokeAsync();
    }

    private async Task OnValueChanged()
    {
        if (!HasManualConfiguredDnsServers && ConfigurationMode is IpConfigurationMode.Manual)
        {
            _dialogVisible = true;
            return;
        }

        await NotifyConfigurationModeChanged();
    }

    private void OnCancel()
    {
        if (!_confirmed)
            ConfigurationMode = _configurationModeBefore;

        _confirmed = false;
    }

    private async Task OnConfirm()
    {
        _confirmed = true;

        await NotifyConfigurationModeChanged();
    }
}
