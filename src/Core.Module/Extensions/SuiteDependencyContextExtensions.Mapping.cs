using Core.Module.Contracts;
using Microsoft.Extensions.DependencyModel;
using Sdk.Modules;

namespace Core.Module.Extensions;

public static partial class SuiteDependencyContextExtensions
{
    internal static SuiteMappingSummary CreateMappingSummary(this SuiteDependencyContext suiteContext)
    {
        var summaries = suiteContext.Mappings.Select(k => k.GetMappingSummary()).ToList();

        return new()
        {
            TotalMappings = suiteContext.Mappings.Sum(k => k.MapFrom.Count),
            Modules = summaries,
        };
    }

    internal static List<ModuleAssemblyMapping> UseAssemblyMapping(this SuiteDependencyContext context)
    {
        var internalMappings = context.UseCoreAssemblyMapping()
            .Concat(context.UseUiHostAssemblyMapping())
            .Concat(context.UsePublicModuleAssemblyMapping())
            .Concat(context.UseSharedAssemblyMapping())
            .Distinct()
            .OrderBy(k => k.AssemblyName);

        context.Mappings.Clear();

        foreach (var mapping in internalMappings)
        {
            var exists = context.Mappings.FirstOrDefault(k => k.AssemblyName == mapping.AssemblyName);
            if (exists is not null)
            {
                exists.MapFrom.Add(new(mapping.MapFromModule, mapping.MapFromVersion));
                continue;
            }

            var map = new ModuleAssemblyMapping(mapping.AssemblyName, new(mapping.MapToModule, mapping.MapToVersion))
            {
                MapFrom = [new(mapping.MapFromModule, mapping.MapFromVersion)],
            };

            context.Mappings.Add(map);
        }

        return context.Mappings;
    }

    private record FlatMapping(string AssemblyName, string MapFromModule, string MapFromVersion, string MapToModule, string MapToVersion);

    private static IEnumerable<FlatMapping> UseCoreAssemblyMapping(this SuiteDependencyContext context)
        => context.UseModulesAssemblyMapping(context.Core, context.UiHosts.Count == 1 ? context.UiHosts[0] : null);

    private static IEnumerable<FlatMapping> UseUiHostAssemblyMapping(this SuiteDependencyContext context)
        => context.UseModulesAssemblyMapping(context.UiHosts.Count == 1 ? context.UiHosts[0] : null);

    private static IEnumerable<FlatMapping> UseModulesAssemblyMapping(this SuiteDependencyContext context,
        ModuleDependencyContext? mapToContext,
        ModuleDependencyContext? optionalMapFromContext = null)
    {
        if (mapToContext is null)
            yield break;

        // the assembly versions of mapToContext libraries will override the module versions
        foreach (var mapToLibrary in mapToContext.RuntimeLibraries)
        {
#if DEBUG
            // only on debugging the project references are listed separately
            if (mapToLibrary.Name.EndsWith(".Reference", StringComparison.Ordinal))
                continue;
#endif
            if (optionalMapFromContext is not null)
            {
                // we ignore the version and redirect to whatever core has
                var mapping = CreateRuntimeMapping(optionalMapFromContext, mapToLibrary);
                if (mapping is not null)
                    yield return mapping;

                var dependencyMapping = CreateDependencyMapping(optionalMapFromContext, mapToLibrary);
                if (dependencyMapping is not null)
                    yield return dependencyMapping;
            }

            foreach (var moduleContext in context.Modules)
            {
                // we ignore the version and redirect to whatever core has
                var mapping = CreateRuntimeMapping(moduleContext, mapToLibrary);
                if (mapping is not null)
                    yield return mapping;

                var dependencyMapping = CreateDependencyMapping(moduleContext, mapToLibrary);
                if (dependencyMapping is not null)
                    yield return dependencyMapping;
            }
        }

        FlatMapping? CreateRuntimeMapping(ModuleDependencyContext mapFromContext, RuntimeLibrary mapToLibrary)
        {
            var mapFromLibrary = mapFromContext.GetRuntimeLibrary(mapToLibrary.Name, null);
            if (mapFromLibrary is null)
                return null;

            // e.g. DevExpress.Blazor will be requested as DevExpress.Blazor.v23.2
            var realAssemblyName = mapFromContext.GetDefaultAssemblyName(mapFromLibrary.Name, mapFromLibrary.Version, false) ?? mapFromLibrary.Name;

            // move the runtime library to redundancy list
            mapFromContext.MarkRendundantLibrary(mapFromLibrary);

            // now we have a library that exists in module and core, but we'll take it from core
            return new(
                realAssemblyName,
                mapFromContext.AssemblyName,
                mapFromLibrary.Version,
                mapToContext.AssemblyName,
                mapToLibrary.Version);
        }

        FlatMapping? CreateDependencyMapping(ModuleDependencyContext mapFromContext, RuntimeLibrary mapToLibrary)
        {
            var dependency = mapFromContext.GetDirectDependency(mapToLibrary.Name, null);
            if (dependency is null)
                return null;

            // now we have a library that exists in module and core, but we'll take it from core
            return new(
                mapToLibrary.Name,
                mapFromContext.AssemblyName,
                dependency.Value.Version,
                mapToContext.AssemblyName,
                mapToLibrary.Version);
        }
    }

    private static IEnumerable<FlatMapping> UsePublicModuleAssemblyMapping(this SuiteDependencyContext context)
    {
        // the assembly versions might differ from assemblies provided by modules it depends on
        // they will be mapped to these
        foreach (var moduleContext in context.Modules.Where(k => k.StartupErrors.Count == 0))
        {
            // direct dependencies -> runtime library. indirect dependencies need no mapping
            var publicDependencies = moduleContext.RuntimeLibraries
                .Where(rt => rt.Name.EndsWith(Constants.ModuleSuffixPublic, StringComparison.Ordinal))
                .Where(rt => !string.IsNullOrEmpty(rt.Name));

            // ".Public" modules will me mapped to their origin backend|client project
            foreach (var dependency in publicDependencies)
            {
                // e.g. ViciOne.Suite.Module.Public -> ViciOne.Suite.Module
                var dependencyContextFamily = dependency.Name.Replace(Constants.ModuleSuffixPublic, string.Empty, StringComparison.Ordinal);

                // prefer backend module as mapping target
                var mapToBackendContext = context.Modules.FirstOrDefault(ctx => ctx.AssemblyName == $"{dependencyContextFamily}{Constants.ModuleSuffixBackend}");
                if (mapToBackendContext is not null)
                {
                    // prevent mapping on itself
                    if (mapToBackendContext.AssemblyName == moduleContext.AssemblyName)
                        continue;

                    var mapToLibrary = mapToBackendContext.RuntimeLibraries.First(rt => rt.Name == dependency.Name);
                    yield return new(
                        dependency.Name,
                        moduleContext.AssemblyName,
                        dependency.Version,
                        mapToBackendContext.AssemblyName,
                        mapToLibrary.Version);
                    continue;
                }

                // might be a module without backend part
                var mapToClientContext = context.Modules.FirstOrDefault(ctx => ctx.AssemblyName == $"{dependencyContextFamily}{Constants.ModuleSuffixClient}");
                if (mapToClientContext is not null)
                {
                    // prevent mapping on itself
                    if (mapToClientContext.AssemblyName == moduleContext.AssemblyName)
                        continue;

                    var mapToLibrary = mapToClientContext.RuntimeLibraries.First(rt => rt.Name == dependency.Name);
                    yield return new(
                        dependency.Name,
                        moduleContext.AssemblyName,
                        dependency.Version,
                        mapToClientContext.AssemblyName,
                        mapToLibrary.Version);
                }
            }
        }
    }

    private record RuntimeLibraryGroupMapper(RuntimeLibrary Library, ModuleDependencyContext Context);

    private static IEnumerable<FlatMapping> UseSharedAssemblyMapping(this SuiteDependencyContext context)
    {
        // handle libraries that are unknown to suite but used by different modules like the
        // ClusterEditor, ScreenDesigner using ViciOne.Ui.TreeEditor.Interface

        // all doubles in modules without context relation
        var doubles = context.GetAllContexts()
            .Where(k => k.StartupErrors.Count == 0)
            .SelectMany(ctx => ctx.RuntimeLibraries.Select(rt => new RuntimeLibraryGroupMapper(rt, ctx)))
            .GroupBy(rt => rt.Library.Name)
            .Where(g => g.Count() > 1 && !g.Key.EndsWith(Constants.ModuleSuffixPublic, StringComparison.Ordinal));

        foreach (var module in doubles)
        {
            // here we have e.g. key: "FastDeepCloner"
            //  -> FastDeepCloner, 1.36.2, ViciOne.Suite.DevExpress.Backend
            //  -> FastDeepCloner, 1.36.2, ViciOne.Suite.ScreenDesigner.Client
            //  -> FastDeepCloner, 1.36.1, ViciOne.Suite.ConnectorEditor.Client
            // the winner will be 1.36.2, ViciOne.Suite.DevExpress.Backend

            // we choose "highest" version but this could fail on code differences between the versions
            var maxVersion = module.MaxBy(k => k.Library.ParseVersion());
            var minVersion = module.MinBy(k => k.Library.ParseVersion());
            var mappingTarget = maxVersion?.Library.Version == minVersion?.Library.Version
                ? GetMapToContext(null) : GetMapToContext(maxVersion?.Library.Version);

            foreach (var item in module)
            {
                // don't map source context to itself
                if (item.Context.AssemblyName == mappingTarget.Context.AssemblyName)
                    continue;

                // move libraries to redundant list
                item.Context.MarkRendundantLibrary(item.Library);

                foreach (var runtimeGroup in item.Library.RuntimeAssemblyGroups)
                {
                    if (item.Library.Type == "reference")
                        continue;

                    foreach (var rtFile in runtimeGroup.RuntimeFiles)
                    {
                        var rtFileName = Path.GetFileNameWithoutExtension(rtFile.Path);
                        if (rtFileName == item.Library.Name)
                            continue;

                        // runtime mappings like MassTransit.EntityFrameworkCore -> MassTransit.EntityFrameworkCoreIntegration
                        yield return new(
                            rtFileName,
                            item.Context.AssemblyName,
                            item.Library.Version,
                            mappingTarget.Context.AssemblyName,
                            mappingTarget.Library.Version);
                    }
                }

                yield return new(
                    item.Library.Name,
                    item.Context.AssemblyName,
                    item.Library.Version,
                    mappingTarget.Context.AssemblyName,
                    mappingTarget.Library.Version);
            }

            RuntimeLibraryGroupMapper GetMapToContext(string? version)
            {
                var backendItem = module.FirstOrDefault(k => k.Context.ModuleType == ModuleType.Backend
                    && (string.IsNullOrEmpty(version) || k.Library.Version == version));
                if (backendItem is not null)
                    return backendItem;

                var clientItem = module.FirstOrDefault(k => k.Context.ModuleType == ModuleType.Client
                    && (string.IsNullOrEmpty(version) || k.Library.Version == version));
                if (clientItem is not null)
                    return clientItem;

                // prefer backend over client
                var backendFallback = module.FirstOrDefault(k => k.Context.ModuleType == ModuleType.Backend);
                if (backendFallback is not null)
                    return backendFallback;

                return module.First();
            }
        }
    }
}
