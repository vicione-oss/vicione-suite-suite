using System.Diagnostics;
using System.IO.Compression;

namespace Core.Shared.Modules;


public static class ModuleZip
{
    public static async Task<byte[]> CreateArchive(Dictionary<string, string> files)
    {
        // create zip file containing assemblies and debug symbols
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            foreach (var file in files)
            {
                await using var fileStream = new FileStream(file.Value, new FileStreamOptions()
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.Read,
                    Share = FileShare.Read,
                    Options = FileOptions.Asynchronous,
                });
                await using var entryStream = archive.CreateEntry(file.Key).Open();

                await fileStream.CopyToAsync(entryStream);
            }
        }

        return memoryStream.ToArray();
    }

    public static Task<byte[]> CreateArchive(string[] files)
    {
        var dic = files.ToDictionary(p => Path.GetFileName(p) ?? throw new InvalidOperationException(), p => p);

        return CreateArchive(dic);
    }

    public static async Task<UnzippedAssemblies> ExtractArchive(byte[] pluginZip, List<string?> excludeAssemblies)
    {
        using var archive = new ZipArchive(new MemoryStream(pluginZip));
        var dlls = new Dictionary<string, byte[]>();
        var pdbs = new Dictionary<string, byte[]>();
        var excluded = new List<string>();

        foreach (var entry in archive.Entries)
        {
            // add only assemblies that are not in current domain
            if (excludeAssemblies.Contains(Path.GetFileNameWithoutExtension(entry.FullName)))
            {
                excluded.Add(entry.FullName);
                continue;
            }

            using var memoryStream = new MemoryStream();
            await entry.Open().CopyToAsync(memoryStream);
            var file = memoryStream.ToArray();
            switch (Path.GetExtension(entry.FullName).ToUpperInvariant())
            {
                case ".DLL":
                    dlls.Add(entry.FullName, file);
                    break;
                case ".PDB":
                    pdbs.Add(entry.FullName, file);
                    break;
            }
        }

        return new UnzippedAssemblies(dlls, pdbs, excluded);
    }

    public static void DumpToConsole(UnzippedAssemblies unzipped, bool full = false)
    {
        Debug.WriteLine("UnzippedAssemblies - import {0} dlls ({1} pdbs). ignore {2} dlls.", unzipped.Dlls.Count, unzipped.Pdbs.Count, unzipped.Ignored.Count);

        if (!full)
            return;

        foreach (var dll in unzipped.Dlls)
        {
            Console.WriteLine("LoadClientAssemblies[Dll]: {0}", dll);
        }
        foreach (var dll in unzipped.Ignored)
        {
            Console.WriteLine("LoadClientAssemblies[Excluded]: {0}", dll);
        }
    }
}

public sealed record UnzippedAssemblies(Dictionary<string, byte[]> Dlls, Dictionary<string, byte[]> Pdbs, List<string> Ignored);
