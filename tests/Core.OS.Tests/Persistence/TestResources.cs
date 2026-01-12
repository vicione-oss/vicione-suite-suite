using System.IO.Abstractions.TestingHelpers;
using System.Reflection;

namespace Core.OS.Tests.Persistence;

internal static class TestResources
{
    public const string BackupZip = "suite-backup.zip";

    public static void AddEmbeddedBackupFile(this MockFileSystem fileSystem, string? backupPath = null)
        => fileSystem.AddFileFromEmbeddedResource(backupPath ?? BackupZip, Assembly.GetExecutingAssembly(), $"Core.OS.Tests.Instance.Resources.{BackupZip}");

    public static byte[] GetEmbeddedBackupFileBytes()
        => GetEmbeddedResourceBytes($"Core.OS.Tests.Instance.Resources.{BackupZip}");

    public static byte[] GetEmbeddedResourceBytes(string embeddedResourcePath)
    {
        var resourceAssembly = Assembly.GetExecutingAssembly();
        ArgumentNullException.ThrowIfNull(resourceAssembly);

        using var embeddedResourceStream = resourceAssembly.GetManifestResourceStream(embeddedResourcePath);
        if (embeddedResourceStream == null)
            throw new ArgumentException("Resource not found in assembly", nameof(embeddedResourcePath));

        using var streamReader = new BinaryReader(embeddedResourceStream);
        return streamReader.ReadBytes((int)embeddedResourceStream.Length);
    }
}
