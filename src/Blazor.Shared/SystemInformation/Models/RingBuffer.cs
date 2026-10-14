namespace Blazor.Shared.SystemInformation.Models;

/// <summary>
/// The latest values, oldest first; the oldest drops out once <see cref="Capacity"/> is reached.
/// </summary>
internal sealed class RingBuffer(int capacity)
{
    private readonly Queue<double> _values = new(capacity);

    public int Capacity { get; } = capacity;

    public void Add(double value)
    {
        if (_values.Count == Capacity)
            _values.Dequeue();

        _values.Enqueue(value);
    }

    /// <summary>
    /// A missing value adds nothing, so a collector without data leaves no gap of zeros.
    /// </summary>
    public void Add(double? value)
    {
        if (value is { } present)
            Add(present);
    }

    public void Clear()
        => _values.Clear();

    public double[] ToArray()
        => [.. _values];

    public MetricHistory ToHistory()
        => new(ToArray(), Capacity);

    /// <summary>
    /// <c>null</c> while empty.
    /// </summary>
    public ValueRange? ToRange()
        => _values.Count == 0 ? null : new(_values.Min(), _values.Average(), _values.Max());
}
