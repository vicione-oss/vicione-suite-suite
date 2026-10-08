using System.IO.Abstractions.TestingHelpers;
using System.IO.Compression;
using System.Reflection;
using HostManagement.Shared.Contracts;

namespace Core.OS.Tests.Persistence;

internal static class TestResources
{
    public const string BackupZip = "suite-backup.zip";
    public const string HostManagement09SystemConfigurationJson = """{"NetworkDNSSettings":{"StaticHosts":[]}}""";
    public const string HostManagement1SystemConfigurationJson = """{"Version":1,"NetworkDNSSettings":{"StaticHosts":[]}}""";

    public static void AddEmbeddedBackupFile(this MockFileSystem fileSystem, string? backupPath = null)
        => fileSystem.AddFileFromEmbeddedResource(backupPath ?? BackupZip, Assembly.GetExecutingAssembly(), $"Core.OS.Tests.Instance.Resources.{BackupZip}");

    public static byte[] GetEmbeddedBackupFileBytes()
        => GetEmbeddedResourceBytes($"Core.OS.Tests.Instance.Resources.{BackupZip}");

    public static byte[] GetEmbeddedBackupFileBytes(string systemConfigurationJson)
    {
        using var backupStream = new MemoryStream();
        backupStream.Write(GetEmbeddedBackupFileBytes());

        using (var archive = new ZipArchive(backupStream, ZipArchiveMode.Update, leaveOpen: true))
        {
            archive.GetEntry(nameof(SystemConfiguration))!.Delete();

            using var writer = new StreamWriter(archive.CreateEntry(nameof(SystemConfiguration)).Open());
            writer.Write(systemConfigurationJson);
        }

        return backupStream.ToArray();
    }

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
