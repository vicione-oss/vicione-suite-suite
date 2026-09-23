using Core.Module.Contracts;
using Microsoft.Extensions.DependencyModel;
using Sdk.Modules;

namespace Core.Module.Extensions;

public static partial class SuiteDependencyContextExtensions
{
    private record FlatMapping(string AssemblyName, string MapFromModule, string MapFromVersion, string MapToModule, string MapToVersion);
    private record RuntimeLibraryGroupMapper(RuntimeLibrary Library, ModuleDependencyContext Context);

    extension(SuiteDependencyContext suiteContext)
    {
        internal SuiteMappingSummary CreateMappingSummary()
        {
            var summaries = suiteContext.Mappings.Select(k => k.GetMappingSummary()).ToList();

            return new()
            {
                TotalMappings = suiteContext.Mappings.Sum(k => k.MapFrom.Count),
                Modules = summaries,
            };
        }

        internal List<ModuleAssemblyMapping> UseAssemblyMapping()
        {
            var internalMappings = suiteContext.UseCoreAssemblyMapping()
                .Concat(suiteContext.UseUiHostAssemblyMapping())
                .Concat(suiteContext.UsePublicModuleAssemblyMapping())
                .Concat(suiteContext.UseSharedAssemblyMapping())
                .Distinct()
                .OrderBy(k => k.AssemblyName);

            suiteContext.Mappings.Clear();

            foreach (var mapping in internalMappings)
            {
                var exists = suiteContext.Mappings.FirstOrDefault(k => k.AssemblyName == mapping.AssemblyName);
                if (exists is not null)
                {
                    exists.MapFrom.Add(new(mapping.MapFromModule, mapping.MapFromVersion));
                    continue;
                }

                var map = new ModuleAssemblyMapping(mapping.AssemblyName, new(mapping.MapToModule, mapping.MapToVersion))
                {
                    MapFrom = [new(mapping.MapFromModule, mapping.MapFromVersion)],
                };

                suiteContext.Mappings.Add(map);
            }

            return suiteContext.Mappings;
        }

        private IEnumerable<FlatMapping> UseCoreAssemblyMapping()
            => suiteContext.UseModulesAssemblyMapping(suiteContext.Core, suiteContext.UiHost);

        private IEnumerable<FlatMapping> UseUiHostAssemblyMapping()
            => suiteContext.UseModulesAssemblyMapping(suiteContext.UiHost);

        private IEnumerable<FlatMapping> UseModulesAssemblyMapping(ModuleDependencyContext? mapToContext,
            ModuleDependencyContext? optionalMapFromContext = null)
        {
            if (mapToContext is null)
                yield break;

            // The mapToContext library versions override the module versions.
            foreach (var mapToLibrary in mapToContext.RuntimeLibraries)
            {
#if DEBUG
                // Project references are listed separately only in a debug build.
                if (mapToLibrary.Name.EndsWith(".Reference", StringComparison.Ordinal))
                    continue;
#endif
                if (optionalMapFromContext is not null)
                {
                    // The version is ignored; the request is redirected to whatever core provides.
                    var mapping = CreateRuntimeMapping(optionalMapFromContext, mapToLibrary);
                    if (mapping is not null)
                        yield return mapping;

                    var dependencyMapping = CreateDependencyMapping(optionalMapFromContext, mapToLibrary);
                    if (dependencyMapping is not null)
                        yield return dependencyMapping;
                }

                foreach (var moduleContext in suiteContext.Modules)
                {
                    // The version is ignored; the request is redirected to whatever core provides.
                    var mapping = CreateRuntimeMapping(moduleContext, mapToLibrary);
                    if (mapping is not null)
                        yield return mapping;

                    var dependencyMapping = CreateDependencyMapping(moduleContext, mapToLibrary);
                    if (dependencyMapping is not null)
                        yield return dependencyMapping;
                }
            }

            yield break;

            FlatMapping? CreateRuntimeMapping(ModuleDependencyContext mapFromContext, RuntimeLibrary mapToLibrary)
            {
                var mapFromLibrary = mapFromContext.GetRuntimeLibrary(mapToLibrary.Name, null);
                if (mapFromLibrary is null)
                    return null;

                // e.g. DevExpress.Blazor is requested as DevExpress.Blazor.v23.2
                var realAssemblyName = mapFromContext.GetDefaultAssemblyName(mapFromLibrary.Name, mapFromLibrary.Version, false) ?? mapFromLibrary.Name;

                // The module copy becomes redundant.
                mapFromContext.MarkRendundantLibrary(mapFromLibrary);

                // The library exists in both module and core; core wins.
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

                // The library exists in both module and core; core wins.
                return new(
                    mapToLibrary.Name,
                    mapFromContext.AssemblyName,
                    dependency.Value.Version,
                    mapToContext.AssemblyName,
                    mapToLibrary.Version);
            }
        }

        private IEnumerable<FlatMapping> UsePublicModuleAssemblyMapping()
        {
            // Assembly versions may differ from those of the modules depended on, and are mapped onto them.
            foreach (var moduleContext in suiteContext.Modules.Where(k => k.StartupErrors.Count == 0))
            {
                // Direct dependencies become runtime libraries; indirect ones need no mapping.
                var publicDependencies = moduleContext.RuntimeLibraries
                    .Where(rt => rt.Name.EndsWith(Constants.ModuleSuffixPublic, StringComparison.Ordinal))
                    .Where(rt => !string.IsNullOrEmpty(rt.Name));

                // A .Public module maps to its originating backend or client project.
                foreach (var dependency in publicDependencies)
                {
                    // e.g. ViciOne.Suite.Module.Public -> ViciOne.Suite.Module
                    var dependencyContextFamily = dependency.Name.Replace(Constants.ModuleSuffixPublic, string.Empty, StringComparison.Ordinal);

                    // The backend module is the preferred mapping target.
                    var mapToBackendContext = suiteContext.Modules.FirstOrDefault(ctx => ctx.AssemblyName == $"{dependencyContextFamily}{Constants.ModuleSuffixBackend}");
                    if (mapToBackendContext is not null)
                    {
                        // A context must not map onto itself.
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

                    // A module may have no backend part.
                    var mapToClientContext = suiteContext.Modules.FirstOrDefault(ctx => ctx.AssemblyName == $"{dependencyContextFamily}{Constants.ModuleSuffixClient}");
                    if (mapToClientContext is not null)
                    {
                        // A context must not map onto itself.
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

        private IEnumerable<FlatMapping> UseSharedAssemblyMapping()
        {
            // Libraries unknown to the suite but shared between modules, e.g. ClusterEditor and
            // ScreenDesigner both using ViciOne.Ui.TreeEditor.Interface.

            // Every duplicate across modules that have no context relation.
            var doubles = suiteContext.GetAllContexts()
                .Where(k => k.StartupErrors.Count == 0)
                .SelectMany(ctx => ctx.RuntimeLibraries.Select(rt => new RuntimeLibraryGroupMapper(rt, ctx)))
                .GroupBy(rt => rt.Library.Name)
                .Where(g => g.Count() > 1 && !g.Key.EndsWith(Constants.ModuleSuffixPublic, StringComparison.Ordinal));

            foreach (var module in doubles)
            {
                // e.g. key "FastDeepCloner"
                //  -> FastDeepCloner, 1.36.2, ViciOne.Suite.DevExpress.Backend
                //  -> FastDeepCloner, 1.36.2, ViciOne.Suite.ScreenDesigner.Client
                //  -> FastDeepCloner, 1.36.1, ViciOne.Suite.ConnectorEditor.Client
                // The winner is 1.36.2, ViciOne.Suite.DevExpress.Backend.

                // The highest version wins, which can break if the versions differ in behaviour.
                var maxVersion = module.MaxBy(k => k.Library.ParseVersion());
                var minVersion = module.MinBy(k => k.Library.ParseVersion());
                var mappingTarget = maxVersion?.Library.Version == minVersion?.Library.Version
                    ? GetMapToContext(null) : GetMapToContext(maxVersion?.Library.Version);

                foreach (var item in module)
                {
                    // A context must not map onto itself.
                    if (item.Context.AssemblyName == mappingTarget.Context.AssemblyName)
                        continue;

                    // The losing copies become redundant.
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

                            // e.g. MassTransit.EntityFrameworkCore -> MassTransit.EntityFrameworkCoreIntegration
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

                    // Fall back to a backend module when no client matched.
                    var backendFallback = module.FirstOrDefault(k => k.Context.ModuleType == ModuleType.Backend);
                    if (backendFallback is not null)
                        return backendFallback;

                    return module.First();
                }
            }
        }
    }
}
