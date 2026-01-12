using Core.Module.Utils;
using Sdk.Modules;

namespace Core.Module.Extensions;

public static partial class SuiteDependencyContextExtensions
{
    internal static void ValidateAssemblyModuleType(this SuiteDependencyContext context)
    {
        var uiHost = context.UiHosts.FirstOrDefault();

        foreach (var module in context.Modules)
        {
            try
            {
                var typeName = module.ModuleType == ModuleType.Backend
                    ? Constants.SdkBackendModuleTypeName
                    : Constants.SdkClientModuleTypeName;

                List<string> components = [module.AssemblyPath];

                // include deps json to provide additional resolver for dlls we removed on publish
                // e.g. ViciOne.Suite.Sdk.Client to find the ClientModule implementations. It's not loaded
                // yet by Core so we need to add the serving ui host resolver
                if (uiHost != null && module.ModuleType == ModuleType.Client)
                {
                    components.Add(uiHost.AssemblyPath);
                }

                // use metadata load context with resolvers for all our module dlls
                using var loadContext = new ModuleMetadataLoadContext(components);

                // load them into the context
                loadContext.LoadAssemblies([module.AssemblyPath]);

                // if we can't load the metadata we won't be able to load the assembly by suite load context
                // could cause a ReflectionTypeLoadException or just can't match the types
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
    /// Validates sdk major + minor version matches referenced modules sdk 
    /// </summary>
    /// <param name="suiteContext"></param>
    /// <exception cref="InvalidOperationException"></exception>
    internal static void ValidateSdkVersion(this SuiteDependencyContext suiteContext)
    {
        var sdkVersionString = suiteContext.Core.GetSdkVersion();
        if (!Version.TryParse(sdkVersionString, out var sdkVersion))
            throw new InvalidOperationException($"Invalid SDK version {sdkVersionString}");

        foreach (var moduleContext in suiteContext.Modules)
        {
            try
            {
                // todo - if could be sure that only major version changes include breaking changes
                // we could assume that 0.17.1 sdk fits to module requirement 0.17.0 for example
                // todo - we could also use the metadata if it's available for all modules
                ModuleVersionValidator.ValidateSdkCompatibility(sdkVersion, moduleContext.GetSdkVersion());
            }
            catch (Exception ex)
            {
                moduleContext.StartupErrors.Add(ex);
            }
        }
    }

    internal static bool IsInvalidModule(this SuiteDependencyContext suiteContext, string moduleAssemblyPath)
    {
        var uiHost = suiteContext.UiHosts.FirstOrDefault(k => k.AssemblyPath == moduleAssemblyPath);
        if (uiHost is not null)
            return false;

        // we assume that context is already validated!
        if (!suiteContext.AreDependenciesValidated)
            suiteContext.ValidateDependencies();

        // e.g. module has an direct error like wrong sdk or is not part of the context
        var moduleContext = suiteContext.Modules.FirstOrDefault(k => k.AssemblyPath == moduleAssemblyPath);
        if (moduleContext is null || moduleContext.StartupErrors.Count > 0)
            return true;

        // get backend for client or client for backend
        // backend has issues client has issues too
        // otherwise the same - one module!
        var linkedContext = suiteContext.Modules
            .FirstOrDefault(k => k.AssemblyFolder == moduleContext.AssemblyFolder
                && k.ModuleType != moduleContext.ModuleType);

        if (linkedContext is not null && linkedContext.StartupErrors.Count > 0)
            return true;

        return false;
    }

    public static void ValidateDependencies(this SuiteDependencyContext suiteContext)
    {
        foreach (var moduleContext in suiteContext.Modules)
        {
            try
            {
                // get references to *.Public projects of other modules
                var dependencies = moduleContext.GetExternalPublicDependencies();

                foreach (var dependency in dependencies)
                {
                    // e.g. ViciOne.Suite.ClusterManagement.Public
                    if (!ModuleHelpers.TryGetFamilyNamePart(dependency.Name, out var commonNamePart))
                        throw new InvalidOperationException($"Can't get dependency key from {dependency.Name}");

                    // required contexts ViciOne.Suite.ClusterManagement.Backend|Client
                    var dependencyContexts = suiteContext.Modules
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

        // e.g. Backend.B references Backend.A -> Backend.A has an StartupError
        // so also Client.B can't work!
        foreach (var backendContext in suiteContext.Modules.Where(k => k.ModuleType == ModuleType.Backend))
        {
            var clientModule = suiteContext.Modules.FirstOrDefault(k => k.ModuleId == backendContext.ModuleId && k.ModuleType == ModuleType.Client);
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

        suiteContext.AreDependenciesValidated = true;
    }
}
