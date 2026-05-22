using Blazor.Shared.Module.Models;
using Core.Shared.Modules.Contracts;
using Sdk.Modules;
using ViciOne.Ui.Blazor.Components.Grid.Services;

namespace Blazor.Shared.Module.ControlPanels.Extensions;

internal static class IGridItemSelectionExtensions
{
    extension(IGridItemSelection<ModuleMetadataModel> selection)
    {
        public IEnumerable<ModulePackageOperation> GetUpdateOperations()
            => selection
                .Distinct()
                .Where(k => !string.IsNullOrEmpty(k.LatestVersion))
                .Select(item => new ModulePackageOperation(new ModuleDependencyPackage
                {
                    Name = item.Name,
                    Version = item.LatestVersion!,
                },
                ModulePackageOperationKind.Install));

        public IEnumerable<ModulePackageOperation> GetInstallOperations()
            => selection
                .Distinct()
                .Select(item => new ModulePackageOperation(new ModuleDependencyPackage
                {
                    Name = item.Name,
                    Version = item.Version,
                },
                ModulePackageOperationKind.Install));

        public IEnumerable<ModulePackageOperation> GetUninstallOperations()
            => selection
                .Distinct()
                .Select(item => new ModulePackageOperation(new ModuleDependencyPackage
                {
                    Name = item.Name,
                    Version = item.Version,
                },
                ModulePackageOperationKind.Uninstall));


        public IEnumerable<ModulePackageOperation> GetRevertOperations()
        {
            // Revert install operations
            var revertInstallOperations = selection
                .Where(k => k.PendingOperation != null && k.PendingOperation.OperationKind == ModulePackageOperationKind.Install)
                .Distinct()
                .Select(item => new ModulePackageOperation(new ModuleDependencyPackage
                {
                    Name = item.Name,
                    Version = item.PendingOperation!.Package.Version,
                },
                ModulePackageOperationKind.Uninstall));

            // Revert uninstall operations
            var revertUninstallOperations = selection
                .Where(k => k.PendingOperation != null && k.PendingOperation.OperationKind == ModulePackageOperationKind.Uninstall)
                .Distinct()
                .Select(item => new ModulePackageOperation(new ModuleDependencyPackage
                {
                    Name = item.Name,
                    Version = item.PendingOperation!.Package.Version,
                },
                ModulePackageOperationKind.Install));

            return revertInstallOperations.Concat(revertUninstallOperations);
        }
    }
}
