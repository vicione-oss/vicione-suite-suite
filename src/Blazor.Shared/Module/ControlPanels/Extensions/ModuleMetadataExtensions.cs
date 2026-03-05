using System.Globalization;
using Blazor.Shared.Module.Models;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Module.ControlPanels.Extensions;

internal static class ModuleMetadataExtensions
{
    public static string GetErrorTooltip(this ModuleMetadataModel? metadata)
    {
        if (metadata == null)
            return string.Empty;

        return string.Join(Environment.NewLine, metadata.Bundle.Errors.Select(e => e.Message));
    }

    public static string GetInstallOrUpdateTooltip(this ModuleMetadataModel? metadata)
    {
        if (metadata == null || metadata.PendingOperation is null)
            return string.Empty;

        return metadata.Installed ?
            string.Format(CultureInfo.InvariantCulture, LibraryManagement.PendingUpdateToSpecificVersion, metadata.PendingOperation.Package.Version) :
            string.Format(CultureInfo.InvariantCulture, LibraryManagement.PendingInstallationOfSpecificVersion, metadata.PendingOperation.Package.Version);
    }

    public static string GetUpdateAvailableTooltip(this ModuleMetadataModel? metadata)
    {
        if (metadata == null || string.IsNullOrEmpty(metadata.LatestVersion))
            return string.Empty;

        return string.Format(CultureInfo.InvariantCulture, LibraryManagement.UpdateToSpecificVersionAvailable, metadata.LatestVersion);
    }

    public static string GetMissingDependenciesTooltip(this ModuleMetadataModel? metadata)
    {
        if (metadata == null || metadata.MissingDependencies.Count == 0)
            return string.Empty;

        var tooltipText = string.Empty;

        foreach (var dependency in metadata.MissingDependencies)
            tooltipText += $"{CommonVocabulary.Requirement} - {CommonVocabulary.InstallVerb} {dependency.Name} v{dependency.Version}{Environment.NewLine}";

        return tooltipText;
    }
}
