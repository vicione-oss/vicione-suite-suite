using Core.Module.Utils;
using Sdk.Modules;
using Semver;

namespace Core.Module.Extensions;

public static partial class SuiteDependencyContextExtensions
{
    extension(SuiteDependencyContext context)
    {
        internal void ValidateAssemblyModuleType()
        {
            foreach (var module in context.Modules)
            {
                try
                {
                    var typeName = module.ModuleType == ModuleType.Backend
                        ? Constants.SdkBackendModuleTypeName
                        : Constants.SdkClientModuleTypeName;

                    List<string> components = [module.AssemblyPath];

                    // Publishing removes dlls such as ViciOne.Suite.Sdk.Client, and Core has not loaded it
                    // yet, so the serving ui host is added as an additional resolver.
                    if (context.UiHost != null && module.ModuleType == ModuleType.Client)
                    {
                        components.Add(context.UiHost.AssemblyPath);
                    }

                    using var loadContext = new ModuleMetadataLoadContext(components);

                    loadContext.LoadAssemblies([module.AssemblyPath]);

                    // Metadata that fails to load here would fail in the suite load context too, either as a
                    // ReflectionTypeLoadException or as types that never match.
                    var loaded = loadContext.Assemblies.FirstOrDefault(assembly => assembly.IsSuiteModule(typeName))
                                 ?? throw new InvalidOperationException($"Assembly '{module.AssemblyPath}' does not contain implementations for '{typeName}'");
                }
                catch (Exception e)
                {
                    module.StartupErrors.Add(e);
                }
            }
        }

        /// <summary>
        /// Validates that the sdk major and minor version match the sdk referenced by the modules.
        /// </summary>
        internal void ValidateSdkVersion()
        {
            var sdkVersionString = context.Core.GetSdkVersion();
            if (!SemVersion.TryParse(sdkVersionString, out var sdkVersion))
                throw new InvalidOperationException($"Invalid SDK version {sdkVersionString}");

            foreach (var moduleContext in context.Modules)
            {
                try
                {
                    ModuleVersionValidator.ValidateSdkCompatibility(sdkVersion, moduleContext.GetSdkVersion());
                }
                catch (Exception ex)
                {
                    moduleContext.StartupErrors.Add(ex);
                }
            }
        }

        internal bool IsInvalidModule(string moduleAssemblyPath)
        {
            if (context.UiHost?.AssemblyPath == moduleAssemblyPath)
                return false;

            if (!context.AreDependenciesValidated)
                context.ValidateDependencies();

            // A direct error such as a wrong sdk, or the module is not part of the context at all.
            var moduleContext = context.Modules.FirstOrDefault(k => k.AssemblyPath == moduleAssemblyPath);
            if (moduleContext is null || moduleContext.StartupErrors.Count > 0)
                return true;

            // The backend and client of one module share a fate: if one has errors, so does the other.
            var linkedContext = context.Modules
                .FirstOrDefault(k => k.AssemblyFolder == moduleContext.AssemblyFolder
                                     && k.ModuleType != moduleContext.ModuleType);

            if (linkedContext is not null && linkedContext.StartupErrors.Count > 0)
                return true;

            return false;
        }

        public void ValidateDependencies()
        {
            foreach (var moduleContext in context.Modules)
            {
                try
                {
                    // References to the *.Public projects of other modules.
                    var dependencies = moduleContext.GetExternalPublicDependencies();

                    foreach (var dependency in dependencies)
                    {
                        // e.g. ViciOne.Suite.ClusterManagement.Public
                        if (!ModuleHelpers.TryGetFamilyNamePart(dependency.Name, out var commonNamePart))
                            throw new InvalidOperationException($"Can't get dependency key from {dependency.Name}");

                        // Required contexts: ViciOne.Suite.ClusterManagement.Backend and .Client
                        var dependencyContexts = context.Modules
                            .Where(k => k.AssemblyName == $"{commonNamePart}{Constants.ModuleSuffixBackend}"
                                        || k.AssemblyName == $"{commonNamePart}{Constants.ModuleSuffixClient}")
                            .ToArray();

                        if (dependencyContexts.Length == 0)
                            throw new InvalidOperationException($"Missing module dependency - install '{dependency.Name}' 'v{dependency.Version}'.");

                        if (dependencyContexts.SelectMany(k => k.StartupErrors).Any())
                            throw new InvalidOperationException($"Module dependency '{dependency.Name}' 'v{dependency.Version}' has errors.");
                    }
                }
                catch (Exception ex)
                {
                    moduleContext.StartupErrors.Add(ex);
                }
            }

            // If Backend.B references a failing Backend.A, Client.B cannot work either.
            foreach (var backendContext in context.Modules.Where(k => k.ModuleType == ModuleType.Backend))
            {
                var clientModule = context.Modules.FirstOrDefault(k => k.ModuleId == backendContext.ModuleId && k.ModuleType == ModuleType.Client);
                if (clientModule is null)
                    continue;

                var merged = backendContext.StartupErrors
                    .UnionBy(clientModule.StartupErrors, k => k.Message)
                    .ToArray();

                backendContext.StartupErrors.Clear();
                clientModule.StartupErrors.Clear();

                backendContext.StartupErrors.AddRange(merged);
                clientModule.StartupErrors.AddRange(merged);
            }

            context.AreDependenciesValidated = true;
        }
    }
}
