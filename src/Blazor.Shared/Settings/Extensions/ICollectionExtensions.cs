namespace Blazor.Shared.Settings.Extensions;

internal static class ICollectionExtensions
{
    /// <returns>A copy of the items, which the collection no longer contains.</returns>
    public static List<T> ToListAndClear<T>(this ICollection<T> collection)
    {
        var result = collection.ToList();
        collection.Clear();

        return result;
    }
}
