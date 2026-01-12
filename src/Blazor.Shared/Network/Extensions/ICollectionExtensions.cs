namespace Blazor.Shared.Network.Extensions;

internal static class ICollectionExtensions
{
    public static void EnsureAtLeastOneItemExists<T>(this ICollection<T> list)
        where T : new()
    {
        if (list.Count == 0)
            list.Add(new T());
    }
}
