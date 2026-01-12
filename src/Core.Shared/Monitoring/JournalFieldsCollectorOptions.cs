namespace Core.Shared.Monitoring;

public class JournalFieldsCollectorOptions
{
    public IReadOnlyCollection<string> Fields { get; set; } = [];

    public StringComparer StringComparer { get; set; } = StringComparer.Ordinal;
}
