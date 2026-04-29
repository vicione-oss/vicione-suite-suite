using System.IO.Compression;
using System.Text.Json;
using Sdk.Messaging;

namespace Core.OS.Instance.Extensions;

internal static class ZipArchiveEntryExtensions
{
    extension(ZipArchiveEntry entry)
    {
        public async Task<TItem?> DeserializeEntry<TItem>(CancellationToken cancellationToken = default)
        {
            await using var stream = await entry.OpenAsync(cancellationToken);

            var result = await JsonSerializer.DeserializeAsync<TItem>(stream, DefaultJsonSerializerSettings.Default, cancellationToken: cancellationToken);

            return result;
        }

        public async Task<long> SerializeToEntry<TItem>(TItem toBeSerialized, CancellationToken cancellationToken = default)
        {
            await using var entryStream = await entry.OpenAsync(cancellationToken);

            await JsonSerializer.SerializeAsync(entryStream, toBeSerialized, DefaultJsonSerializerSettings.Default, cancellationToken);

            return entryStream.Position;
        }
    }
}
