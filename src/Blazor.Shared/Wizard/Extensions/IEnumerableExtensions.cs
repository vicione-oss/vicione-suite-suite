using System.Diagnostics.CodeAnalysis;

namespace Blazor.Shared.Wizard.Extensions;

internal static class IEnumerableExtensions
{
    /// <summary>
    /// Reduces <paramref name="items"/> beginning at <paramref name="start"/>, then moving to
    /// the next item, then to the previous item, then repeating the process until all items
    /// have been touched.
    /// 
    /// If <paramref name="maximumItems"/> is reached and move to next / previous item is
    /// still possible, then the last collected item is discarded and replaced with
    /// the next / previous item.
    /// </summary>
    /// <remarks>
    /// Example: (1), 2, 3, 4, [5], 6, 7, 8, 9, 10, 11, (12) is reduced to (1), 4, [5], 6, 7, (12)</remarks>
    /// <param name="start">Item where the reduce operation should start from</param>
    /// <returns>Reduced items</returns>
    /// <exception cref="ArgumentException"/>
    public static HashSet<T> Reduce<T>(this IEnumerable<T> items, T start, int maximumItems)
    {
        var items_ = items.ToList();

        // Example (start = 5, itemCount = 12, maximum items = 6):
        //  1 2 3 4 [5] 6 7 8 9 10 11 12
        //
        // State of result from start to finish:
        //  5
        //  5 6
        //  4 5 6 
        //  4 5 6 7
        //  3 4 5 6 7
        //  3 4 5 6 7 8 <- maximum items reached
        //  2 4 5 6 7 8 <- 3 replaced with 2
        //  2 4 5 6 7 9 <- 8 replaced with 9
        //  1 4 5 6 7 9 <- 2 replaced with 1
        //  1 4 5 6 7 10 <- 9 replaced with 10
        //  1 4 5 6 7 11 <- 10 replaced with 11
        //  1 4 5 6 7 12 <- 11 replaced with 12

        var startIndex = items_.IndexOf(start);

        if (startIndex == -1)
            throw new ArgumentException("Start not found", nameof(start));

        var result = new HashSet<T>
        {
            start
        };

        var previousCursor = new ItemCursor<T>(items_, startIndex, moveInterval: -1);
        previousCursor.Move();

        var nextItemCursor = new ItemCursor<T>(items_, startIndex, moveInterval: +1);
        nextItemCursor.Move();

        while (previousCursor.IsValid || nextItemCursor.IsValid)
        {
            if (nextItemCursor.IsValid)
            {
                if (result.Count == maximumItems && nextItemCursor.TryGetItemBefore(out var itemBefore))
                    result.Remove(itemBefore);

                result.Add(nextItemCursor.Item);

                nextItemCursor.Move();
            }

            if (previousCursor.IsValid)
            {
                if (result.Count == maximumItems && previousCursor.TryGetItemBefore(out var itemBefore))
                    result.Remove(itemBefore);

                result.Add(previousCursor.Item);

                previousCursor.Move();
            }
        }

        return result;
    }

    private ref struct ItemCursor<T>(List<T> items, int itemIndex, int moveInterval)
    {
        private int? _indexBefore;

        public bool IsValid { get; private set; } = itemIndex >= 0 && itemIndex < items.Count;
        public int Index { get; private set; } = itemIndex;

        public readonly T Item => items[Index];

        public void Move()
        {
            _indexBefore = Index;

            Index += moveInterval;

            IsValid = Index >= 0 && Index < items.Count;
        }

        public readonly bool TryGetItemBefore([MaybeNullWhen(false)] out T itemBefore)
        {
            if (_indexBefore.HasValue)
            {
                itemBefore = items[_indexBefore.Value];

                return true;
            }
            else
            {
                itemBefore = default;

                return false;
            }
        }
    }
}
