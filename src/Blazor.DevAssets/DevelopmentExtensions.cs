using System.IO.Abstractions;
using Core.UiHosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Sdk.Client.Modules;

namespace Blazor.DevAssets;

public static partial class DevelopmentExtensions
{
    extension(IWebHostEnvironment hostEnvironment)
    {
        /// <summary>
        /// Extends the WebRootFileProvider with the published wwwroot and one provider per module, so the
        /// web server resolves every asset request.
        /// </summary>
        public IWebHostEnvironment UseClientAssetsProduction(IUiHostEnvironment uiEnvironment, ILogger logger)
        {
            hostEnvironment.ContentRootPath = GetUiHostModulePath(uiEnvironment.ModulePath);
            hostEnvironment.WebRootPath = Path.Combine(hostEnvironment.ContentRootPath, "wwwroot");

            var wwwRootFolder = uiEnvironment.GetWwwRootFolder();
            if (!Directory.Exists(wwwRootFolder))
                throw new DirectoryNotFoundException(wwwRootFolder);

            var fileProviders = new List<IFileProvider>
            {
                hostEnvironment.WebRootFileProvider,
                new PhysicalFileProvider(wwwRootFolder)
            };

            // Each module provider serves /_content/{ModuleDllName}.
            foreach (var pathInfo in uiEnvironment.GetModulesContentPathInfos())
            {
                var moduleName = Path.GetFileNameWithoutExtension(pathInfo.Key.AssemblyPath);
                fileProviders.Add(new ModuleFileProvider(moduleName, pathInfo.Value, ModuleAssetHelper.ContentPrefix));

                LogModuleProviderAdded(logger, pathInfo.Key, pathInfo.Value);
            }

            // Works for the published client, e.g. path\to\publish\Client\wwwroot.
            hostEnvironment.WebRootFileProvider = new CompositeFileProvider(fileProviders);

            LogUsingProductionWwwRoot(logger, wwwRootFolder, fileProviders.Count - 2);

            return hostEnvironment;
        }

        /// <summary>
        /// Extends the WebRootFileProvider with a <see cref="DevelopmentFileProvider"/> built from the
        /// *staticwebassets.runtime.json files, so assets resolve from the debug build output.
        /// </summary>
        public IWebHostEnvironment UseClientAssetsDevelopment(IUiHostEnvironment uiEnvironment, IFileSystem fileSystem,
            ILogger logger)
        {
            hostEnvironment.SetWebPaths(uiEnvironment);

            var wwwRootFolder = uiEnvironment.GetWwwRootFolder();
            if (!fileSystem.Directory.Exists(wwwRootFolder))
                throw new DirectoryNotFoundException($"WwwRoot-Path:{wwwRootFolder}");

            LogUsingDevelopmentWwwRoot(logger, wwwRootFolder);

            var contentProvider = new DevelopmentFileProvider(fileSystem);
            var fileProviders = new List<IFileProvider>
            {
                hostEnvironment.WebRootFileProvider,
                contentProvider
            };

            // wwwRoot is the client folder under WASM and the UI host debug folder under Server.
            contentProvider.AddStaticWebAssetJsonsFromPath(wwwRootFolder);

            foreach (var pathInfo in uiEnvironment.GetModulesContentPathInfos().Where(k => k.Key.IsDebugSource))
            {
                contentProvider.AddStaticWebAssetJsonsFromPath(pathInfo.Value);

                LogModuleWebAssetsAdded(logger, pathInfo.Key, pathInfo.Value);
            }

            // Each module provider serves /_content/{ModuleDllName}.
            foreach (var item in uiEnvironment.GetModulesContentPathInfos().Where(k => !k.Key.IsDebugSource))
            {
                var moduleName = Path.GetFileNameWithoutExtension(item.Key.AssemblyPath);
                fileProviders.Add(new ModuleFileProvider(moduleName, item.Value, ModuleAssetHelper.ContentPrefix));

                LogModuleProviderAdded(logger, item.Key, item.Value);
            }

            hostEnvironment.WebRootFileProvider = new CompositeFileProvider(fileProviders);

            return hostEnvironment;
        }

        private void SetWebPaths(IUiHostEnvironment uiEnv)
        {
            string? uiHostModulePath = null;

            // ModulePath is bin/Debug/netX.0, so the project folder is two levels up.
            if (uiEnv.ModulePath?.Contains("Debug", StringComparison.Ordinal) == true)
            {
                var debug = Directory.GetParent(uiEnv.ModulePath);
                uiHostModulePath = debug?.Parent?.Parent?.FullName;
            }

            hostEnvironment.ContentRootPath = GetUiHostModulePath(uiHostModulePath);
            hostEnvironment.WebRootPath = Path.Combine(hostEnvironment.ContentRootPath, "wwwroot");
        }
    }

    private static string GetUiHostModulePath(string? modulePath) => modulePath ?? throw new InvalidOperationException("UiHost - content root path is invalid.");

    [LoggerMessage(LogLevel.Debug, "Added module provider key:{Key} from {Folder}")]
    private static partial void LogModuleProviderAdded(ILogger logger, ModulePathInfo key, string folder);

    [LoggerMessage(LogLevel.Debug, "Added static module webassets for {Key} from {Folder}")]
    private static partial void LogModuleWebAssetsAdded(ILogger logger, ModulePathInfo key, string folder);

    [LoggerMessage(LogLevel.Information, "Using client production wwwroot '{Folder}' (+ {Count} module content paths)")]
    private static partial void LogUsingProductionWwwRoot(ILogger logger, string folder, int count);

    [LoggerMessage(LogLevel.Information, "Using client development wwwroot '{Folder}'")]
    private static partial void LogUsingDevelopmentWwwRoot(ILogger logger, string folder);
}
