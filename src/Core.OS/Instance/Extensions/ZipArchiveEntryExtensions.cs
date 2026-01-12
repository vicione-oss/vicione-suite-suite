using System.IO.Compression;
using System.Text.Json;
using Sdk.Messaging;

namespace Core.OS.Instance.Extensions;

internal static class ZipArchiveEntryExtensions
{
    public static async Task<TItem?> DeserializeEntry<TItem>(this ZipArchiveEntry entry, CancellationToken cancellationToken = default)
    {
        await using var stream = entry.Open();
        return await JsonSerializer.DeserializeAsync<TItem>(stream, DefaultJsonSerializerSettings.Default, cancellationToken: cancellationToken);
    }

    public static async Task<long> SerializeToEntry<TItem>(this ZipArchiveEntry entry, TItem toBeSerialized, CancellationToken cancellationToken = default)
    {
        await using var entryStream = entry.Open();
        await JsonSerializer.SerializeAsync(entryStream, toBeSerialized, DefaultJsonSerializerSettings.Default, cancellationToken);
        return entryStream.Position;
    }
}
