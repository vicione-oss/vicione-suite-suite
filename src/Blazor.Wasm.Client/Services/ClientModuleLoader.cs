using System.Globalization;
using System.Runtime.Loader;
using Blazor.Wasm.Client.Infrastructure.Modules;
using Core.Shared.Modules;

namespace Blazor.Wasm.Client.Services;

public static class ClientModuleLoader
{
    public static async Task<ClientModuleLoaderResult> LoadClientAssemblies(IBackendModuleHttpClient http)
    {
        // get list of loaded assemblies on the client
        var assemblies = GetCurrentDomainAssemblyNames().ToList();

        return await LoadAssemblies(_ => http.LoadClientModulesArchive(assemblies), assemblies);
    }


    public static async Task<ClientModuleLoaderResult> LoadClientResourceAssemblies(IBackendModuleHttpClient http, CultureInfo info)
    {
        // get list of loaded assemblies on the client
        var assemblies = GetCurrentDomainAssemblyNames().ToList();

        return await LoadAssemblies(_ => http.LoadClientModuleResourcesArchive(info), assemblies);
    }

    private static async Task<ClientModuleLoaderResult> LoadAssemblies(Func<IEnumerable<string?>, Task<byte[]>> loadModuleZip, List<string?> loadedAssemblies)
    {
        var result = new ClientModuleLoaderResult();
        try
        {
            // get assemblies from server and load into client app domain
            var zip = await loadModuleZip(loadedAssemblies);

            // if our request does not contain our Identity cookie we'll get an empty array
            if (zip.Length <= 0)
            {
                return result;
            }

            result.ZipLength = zip.Length;

            // assemblies and debug symbols are packaged in a zip file
            var unzipped = await ModuleZip.ExtractArchive(zip, loadedAssemblies);
            result.UnzippedDllCount = unzipped.Dlls.Count;

            ModuleZip.DumpToConsole(unzipped);

            LoadIntoContext(unzipped, result);
        }
        catch (Exception e)
        {
            result.Error = e;
        }

        return result;
    }

    public static IEnumerable<string?> GetCurrentDomainAssemblyNames()
        => AssemblyLoadContext.Default.Assemblies
            .Select(a => a.GetName().Name)
            .OrderBy(k => k);

    private static void LoadIntoContext(UnzippedAssemblies unzipped, ClientModuleLoaderResult result)
    {
        foreach (var item in unzipped.Dlls)
        {
            try
            {
                var pdbKey = Path.ChangeExtension(item.Key, ".pdb");
                if (unzipped.Pdbs.TryGetValue(pdbKey, out var pdb))
                {
                    using var dllStream = new MemoryStream(item.Value);
                    using var pdbStream = new MemoryStream(pdb);

                    AssemblyLoadContext.Default.LoadFromStream(dllStream, pdbStream);
                }
                else
                {
                    using var dllStream = new MemoryStream(item.Value);
                    AssemblyLoadContext.Default.LoadFromStream(dllStream);
                }

                result.LoadedDlls.Add(item.Key);
            }
            catch (Exception e)
            {
                result.LoadErrors.TryAdd(item.Key, e.Message);
            }
        }
    }
}
