using AwesomeAssertions;
using Blazor.Shared.Module.ControlPanels.Services;
using Blazor.Shared.Module.Models;
using Core.Shared.Modules.Contracts;
using Core.Shared.Modules.Events;
using Sdk.Messaging;
using Sdk.Modules;
using Xunit;

namespace Blazor.Shared.Tests.Module.ControlPanels.Services;

public class ModuleManagementControlPanelStateTests
{
    private static readonly ModuleDependencyPackage _package = new()
    {
        Name = "ViciOne.TestPackage",
        Version = "1.0.0"
    };

    private readonly ModuleMetadataBundle _installedBundle = new()
    {
        ModuleId = "ViciOne.TestPackage",
        Metadata = new ModuleMetadata() { MinSuiteSdkVersion = "1.0.0", Name = _package.Name, Version = _package.Version },
        Installed = true,
    };

    private ModuleMetadataModel CreateInstalledBundle(ModulePackageOperation? operation = null) => new()
    {
        Bundle = _installedBundle,
        Installed = true,
        EditOptions = [],
        HasModifiedOptions = true,
        PendingOperation = operation,
    };

    public sealed class ApplyOperationChanges : ModuleManagementControlPanelStateTests
    {
        [Fact]
        public void Should_set_pending_operation_on_installed_package()
        {
            // Arrange
            var package = CreateInstalledBundle();
            var state = new ModuleManagementControlPanelState
            {
                InstalledModules = new List<ModuleMetadataModel> { package }.AsQueryable()
            };
            var operation = new ModulePackageOperation(_package, ModulePackageOperationKind.Install);
            var change = new ModulePackageChange(CrudAction.Created, operation);

            // Act
            var result = state.ApplyOperationChanges(new ModulePackageOperationsChanged([change]));

            // Assert
            result.Should().BeTrue();
            package.PendingOperation.Should().Be(operation);
        }

        [Fact]
        public void Should_clear_pending_install_operation_with_uninstall_operation()
        {
            // Arrange
            var package = CreateInstalledBundle(new ModulePackageOperation(_package, ModulePackageOperationKind.Install));
            var state = new ModuleManagementControlPanelState
            {
                InstalledModules = new List<ModuleMetadataModel> { package }.AsQueryable()
            };

            var operation = new ModulePackageOperation(_package, ModulePackageOperationKind.Uninstall);
            var change = new ModulePackageChange(CrudAction.Deleted, operation);

            // Act
            var result = state.ApplyOperationChanges(new ModulePackageOperationsChanged([change]));

            // Assert
            result.Should().BeTrue();
            package.PendingOperation.Should().BeNull();
        }

        [Fact]
        public void Should_clear_pending_uninstall_operation_with_install_operation()
        {
            // Arrange
            var package = CreateInstalledBundle(new ModulePackageOperation(_package, ModulePackageOperationKind.Uninstall));
            var state = new ModuleManagementControlPanelState
            {
                InstalledModules = new List<ModuleMetadataModel> { package }.AsQueryable()
            };

            var operation = new ModulePackageOperation(_package, ModulePackageOperationKind.Install);
            var change = new ModulePackageChange(CrudAction.Deleted, operation);

            // Act
            var result = state.ApplyOperationChanges(new ModulePackageOperationsChanged([change]));

            // Assert
            result.Should().BeTrue();
            package.PendingOperation.Should().BeNull();
        }

        [Fact]
        public void Should_set_pending_operation_on_available_package_when_not_in_installed()
        {
            // Arrange
            var package = CreateInstalledBundle();
            var state = new ModuleManagementControlPanelState
            {
                AvailableModules = new List<ModuleMetadataModel> { package }.AsQueryable()
            };
            var operation = new ModulePackageOperation(_package, ModulePackageOperationKind.Install);
            var change = new ModulePackageChange(CrudAction.Deleted, operation);

            // Act
            var result = state.ApplyOperationChanges(new ModulePackageOperationsChanged([change]));

            // Assert
            result.Should().BeTrue();
            package.PendingOperation.Should().Be(operation);
        }

        [Fact]
        public void Should_return_false_when_no_matching_package_found()
        {
            // Arrange
            var state = new ModuleManagementControlPanelState();
            var operation = new ModulePackageOperation(_package, ModulePackageOperationKind.Install);
            var change = new ModulePackageChange(CrudAction.Created, operation);

            // Act
            var result = state.ApplyOperationChanges(new ModulePackageOperationsChanged([change]));

            // Assert
            result.Should().BeFalse();
        }
    }

    public sealed class UpdateInstallOperations : ModuleManagementControlPanelStateTests
    {
        [Fact]
        public void Should_add_install_operation_for_available_packages()
        {
            // Arrange
            var state = new ModuleManagementControlPanelState();
            var operation = new ModulePackageOperation(_package, ModulePackageOperationKind.Install);

            // Act
            state.UpdateUninstallOperations([operation]);

            // Assert
            state.UninstallOperations.Should().ContainSingle(op =>
                op.Package.Name == _package.Name &&
                op.OperationKind == ModulePackageOperationKind.Install);
        }

        [Fact]
        public void Should_not_add_duplicate_operations()
        {
            // Arrange
            var state = new ModuleManagementControlPanelState();
            var operation = new ModulePackageOperation(_package, ModulePackageOperationKind.Install);
            state.UpdateInstallOperations([operation]);

            // Act
            state.UpdateInstallOperations([operation]);

            // Assert
            state.InstallOperations.Should().HaveCount(1);
        }
    }

    public sealed class UpdateUninstallOperations : ModuleManagementControlPanelStateTests
    {
        [Fact]
        public void Should_add_uninstall_operation_for_installed_packages()
        {
            // Arrange
            var state = new ModuleManagementControlPanelState();
            var operation = new ModulePackageOperation(_package, ModulePackageOperationKind.Uninstall);

            // Act
            state.UpdateUninstallOperations([operation]);

            // Assert
            state.UninstallOperations.Should().ContainSingle(op =>
                op.Package.Name == _package.Name &&
                op.OperationKind == ModulePackageOperationKind.Uninstall);
        }

        [Fact]
        public void Should_not_add_duplicate_operations()
        {
            // Arrange
            var state = new ModuleManagementControlPanelState
            {
                InstalledModules = new List<ModuleMetadataModel> { CreateInstalledBundle() }.AsQueryable()
            };

            var operation = new ModulePackageOperation(_package, ModulePackageOperationKind.Uninstall);
            state.UpdateUninstallOperations([operation]);

            // Act
            state.UpdateUninstallOperations([operation]);

            // Assert
            state.UninstallOperations.Should().HaveCount(1);
        }
    }

    public sealed class HasPendingChanges : ModuleManagementControlPanelStateTests
    {
        [Fact]
        public void Should_return_false_when_all_lists_are_empty()
        {
            var state = new ModuleManagementControlPanelState();

            state.HasPendingChanges().Should().BeFalse();
        }

        [Fact]
        public void Should_return_true_when_install_operations_are_present()
        {
            var state = new ModuleManagementControlPanelState();
            state.InstallOperations.Add(new ModulePackageOperation(_package, ModulePackageOperationKind.Install));

            state.HasPendingChanges().Should().BeTrue();
        }

        [Fact]
        public void Should_return_true_when_uninstall_operations_are_present()
        {
            var state = new ModuleManagementControlPanelState();
            state.UninstallOperations.Add(new ModulePackageOperation(_package, ModulePackageOperationKind.Uninstall));

            state.HasPendingChanges().Should().BeTrue();
        }
    }
}
