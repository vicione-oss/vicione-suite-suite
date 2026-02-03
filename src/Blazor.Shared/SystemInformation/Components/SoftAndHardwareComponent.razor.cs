using System.Globalization;
using Blazor.Shared.SystemInformation.Extensions;
using Blazor.Shared.SystemInformation.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Instance;

namespace Blazor.Shared.SystemInformation.Components;

public sealed partial class SoftAndHardwareComponent
{
    private const string NotAvailable = "n/a";

    public string Processor { get; private set; } = NotAvailable;
    public string Memory { get; private set; } = NotAvailable;
    public string Disk { get; private set; } = NotAvailable;
    public string SuiteVersion { get; private set; } = NotAvailable;
    public string SerialNumber { get; private set; } = NotAvailable;
    public string? BranchName { get; private set; }
    public string? MachineName { get; private set; }

    [Inject]
    public IInstanceInformationProvider InformationProvider { get; set; } = default!;

    [Inject]
    public HardwareInfoService HardwareInformationProvider { get; set; } = default!;

    protected override void OnInitialized()
    {
        if (!string.IsNullOrEmpty(HardwareInformationProvider.Processor))
            Processor = HardwareInformationProvider.Processor;
        if (HardwareInformationProvider.TotalMemoryKb > 0)
            Memory = HardwareInfoService.FormatNumber(HardwareInformationProvider.TotalMemoryKb, CultureInfo.CurrentUICulture);
        if (HardwareInformationProvider.TotalDiskSizeGb > 0)
            Disk = HardwareInfoService.FormatNumber(HardwareInformationProvider.TotalDiskSizeGb * 1024 * 1024, CultureInfo.CurrentUICulture);
        if (!string.IsNullOrEmpty(InformationProvider.Local.SerialNumber))
            SerialNumber = InformationProvider.Local.SerialNumber;
        if (!string.IsNullOrEmpty(InformationProvider.Local.Version))
            SuiteVersion = InformationProvider.Local.Version;
        BranchName = InformationProvider.Local.BranchName;
        MachineName = Environment.MachineName;

        base.OnInitialized();
    }

    private Task<string> CopyToClipboardButtonSetText() => InformationProvider.GetSystemInformationMarkdown();
}
