using System.Globalization;
using ViciOne.SystemMonitoring;
using ViciOne.SystemMonitoring.Metrics;
using ViciOne.Ui.Localization.Extensions;

namespace Blazor.Shared.SystemInformation.Services;

public sealed class HardwareInfoService : IOutput
{
    private ushort _isInitialized;

    public double TotalMemoryKb { get; private set; }
    public double TotalDiskSizeGb { get; private set; }
    public string Processor { get; private set; } = string.Empty;

    public Task Connect(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task Disconnect(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task Publish(IReadOnlyCollection<Metric> metrics, DateTimeOffset timestamp, CancellationToken cancellationToken)
    {
        if (_isInitialized == 3)
            return Task.CompletedTask;

        var totalMemoryMetric = metrics.FirstOrDefault(m => m is { Group: "ram", Name: "total" });
        if (TotalMemoryKb == default && totalMemoryMetric is not null)
        {
            TotalMemoryKb = (double)totalMemoryMetric.Value;
            _isInitialized++;
        }

        var diskSizeMetric = metrics.FirstOrDefault(m => m is { Group: "disk", SubGroup: "\\", Name: "totalSizeGB" });
        if (TotalDiskSizeGb == default && diskSizeMetric is not null)
        {
            TotalDiskSizeGb = (double)diskSizeMetric.Value;
            _isInitialized++;
        }

        var processorMetric = metrics.FirstOrDefault(m => m is { Group: "processor", Name: "processor" });
        if (string.IsNullOrEmpty(Processor) && processorMetric is not null)
        {
            Processor = RemoveAdditionalSigns((string)processorMetric.Value);
            _isInitialized++;
        }

        return Task.CompletedTask;
    }

    internal static string FormatNumber(double kbValue, CultureInfo cultureInfo)
    {
        var bytes = kbValue > 0 ? Convert.ToInt64(kbValue * 1024) : 0L;

        return bytes.LocalizeFileSizeHumanReadable(cultureInfo, "F1");
    }

    internal static string RemoveAdditionalSigns(string value)
        => value
            .Replace(" limited", "", StringComparison.InvariantCultureIgnoreCase)
            .Replace("(R)", "", StringComparison.InvariantCultureIgnoreCase)
            .Replace("(TM)", "", StringComparison.InvariantCultureIgnoreCase);
}
