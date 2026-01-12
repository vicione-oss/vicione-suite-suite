namespace Blazor.Shared.Wizard.Comparers;

// todo: try to unify with Settings.Comparers.OptionalPositionComparer
internal sealed class OptionalPositionComparer : IComparer<int?>
{
    public int Compare(int? x, int? y)
    {
        // Elements with no position should come last

        if (x is null && y is null)
            return 0;

        if (x is null)
            return 1;

        if (y is null)
            return -1;

        return Comparer<int>.Default.Compare(x.Value, y.Value);
    }
}
