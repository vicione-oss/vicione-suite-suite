using System.Diagnostics.CodeAnalysis;
using Core.Module.Contracts;
using Core.Module.Utils;

namespace Core.Module.Extensions;

public static class ModuleAssemblyMappingExtensions
{
    internal static ModuleMappingSummary GetMappingSummary(this ModuleAssemblyMapping mapping)
    {
        // all mappings that match to x.x.x (additional versioning ignored - )
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

        // mappings that match to x.x except patchMappings
        var buildDiff = mapping.MapFrom.Except(matches)
            .Where(k =>
            {
                if (!TryParseVersions(k.Version, mapping.MapTo.Version, out var from, out var to))
                    return k.Version == mapping.MapTo.Version;

                return from.Major == to.Major
                    && from.Minor == to.Minor;
            })
            .ToList();

        // mappings that match to x except patchMappings
        var minorDiffs = mapping.MapFrom.Except(matches)
            .Except(buildDiff)
            .Where(k =>
            {
                if (!TryParseVersions(k.Version, mapping.MapTo.Version, out var from, out var to))
                    return k.Version == mapping.MapTo.Version;

                return from.Major == to.Major;
            })
            .ToList();

        // here the version is totally different like 3.x.x and 7.x.x
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
