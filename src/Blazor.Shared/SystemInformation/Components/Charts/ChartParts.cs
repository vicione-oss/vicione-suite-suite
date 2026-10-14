namespace Blazor.Shared.SystemInformation.Components.Charts;

public enum ChartEmphasis
{
    Normal,
    Focus,
    Dim,
}

/// <summary>
/// A line over the slots of a <see cref="LoadChart"/>.
/// </summary>
/// <param name="CssClass">Names the series, such as <c>cpu</c>, and so its color.</param>
/// <param name="Values">One value per slot on the chart's scale; <c>null</c> leaves a gap.</param>
public sealed record ChartLine(string CssClass, IReadOnlyList<double?> Values, ChartEmphasis Emphasis = ChartEmphasis.Normal);

/// <summary>
/// An area between a low and a high value per slot, such as the lowest and highest reading within each minute.
/// </summary>
public sealed record ChartBand(string CssClass, IReadOnlyList<double?> Low, IReadOnlyList<double?> High);

/// <summary>
/// A range across the whole width, such as the lowest to the highest value of the span, with an optional line inside it.
/// </summary>
public sealed record ChartLevel(string CssClass, double Low, double High, double? Line);

/// <summary>
/// A point the user can select, such as a peak.
/// </summary>
public sealed record ChartMarker(int Slot, double Value, string CssClass, string Label);

/// <summary>
/// A stretch of slots shown in the background, such as the minutes a peak lasted.
/// </summary>
public sealed record ChartRange(int FromSlot, int ToSlot);
