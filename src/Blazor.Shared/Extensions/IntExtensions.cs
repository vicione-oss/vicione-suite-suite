using System.Globalization;

namespace Blazor.Shared.Extensions;

internal static class IntExtensions
{
    extension(int number)
    {
        /// <summary>
        /// Formats the number with the invariant culture, followed by <paramref name="addition"/>, e.g. <c>-100%</c> for a style attribute.
        /// </summary>
        public string ToAttributeValue(string addition = "")
            => string.Create(CultureInfo.InvariantCulture, $"{number}{addition}");
    }
}
