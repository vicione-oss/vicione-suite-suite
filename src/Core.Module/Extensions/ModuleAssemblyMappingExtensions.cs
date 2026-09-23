using System.Diagnostics.CodeAnalysis;
using Core.Module.Contracts;
using Core.Module.Utils;

namespace Core.Module.Extensions;

public static class ModuleAssemblyMappingExtensions
{
    internal static ModuleMappingSummary GetMappingSummary(this ModuleAssemblyMapping mapping)
    {
        // All mappings matching x.x.x, ignoring any additional versioning.
        var matches = mapping.MapFrom
            .Where(k =>
            {
                if (!TryParseVersions(k.Version, mapping.MapTo.Version, out var from, out var to))
                    return k.Version == mapping.MapTo.Version;

                return from.Major == to.Major
                    && from.Minor == to.Minor
                    && from.Build == to.Build;
            })
            .ToList();

        // Mappings matching x.x, excluding patchMappings.
        var buildDiff = mapping.MapFrom.Except(matches)
            .Where(k =>
            {
                if (!TryParseVersions(k.Version, mapping.MapTo.Version, out var from, out var to))
                    return k.Version == mapping.MapTo.Version;

                return from.Major == to.Major
                    && from.Minor == to.Minor;
            })
            .ToList();

        // Mappings matching x, excluding patchMappings.
        var minorDiffs = mapping.MapFrom.Except(matches)
            .Except(buildDiff)
            .Where(k =>
            {
                if (!TryParseVersions(k.Version, mapping.MapTo.Version, out var from, out var to))
                    return k.Version == mapping.MapTo.Version;

                return from.Major == to.Major;
            })
            .ToList();

        // The versions differ entirely here, e.g. 3.x.x against 7.x.x.
        var majorDiffs = mapping.MapFrom
            .Except(matches)
            .Except(buildDiff)
            .Except(minorDiffs)
            .ToList();

        return new()
        {
            AssemblyName = mapping.AssemblyName,
            MapTo = mapping.MapTo,
            Matches = matches,
            BuildDiffs = buildDiff,
            MinorDiffs = minorDiffs,
            MajorDiffs = majorDiffs,
        };

        static bool TryParseVersions(string mapFromVersion, string mapToVersion, [NotNullWhen(true)] out Version? from, [NotNullWhen(true)] out Version? to)
        {
            if (VersionUtils.TryParseVersion(mapFromVersion, out var mapFromParsed)
                && VersionUtils.TryParseVersion(mapToVersion, out var mapToParsed))
            {
                from = mapFromParsed;
                to = mapToParsed;
                return true;
            }
            from = to = null;
            return false;
        }
    }
}
