using Microsoft.Extensions.Configuration;

namespace Core.Shared.Monitoring;

public sealed class CollectorOptions
{
    public bool Cpu { get; set; } = true;

    public bool Ram { get; set; } = true;

    public bool Disk { get; set; } = true;

    public bool Network { get; set; } = true;

    public bool Process { get; set; } = true;

    public bool Processor { get; set; } = true;

    [ConfigurationKeyName(Collectors.ThermalSensors)]
    public bool ThermalSensors { get; set; } = true;

    public bool Uptime { get; set; } = true;

    public bool JournalFields { get; set; } = true;

    public bool LoadAvg { get; set; } = true;
}
