using Core.OS.Instance.Initialization;

namespace Core.OS.Tests.Instance.Initialization;

public class SyncDataHelpersTests
{
    [Fact]
    public void WithAlwaysSyncedModules_should_add_the_system_module_when_missing()
    {
        // Arrange
        var installedModules = new[] { "ModuleA", "ModuleB" };

        // Act
        var result = SyncDataHelpers.WithAlwaysSyncedModules(installedModules).ToList();

        // Assert
        result.Should().Equal("ModuleA", "ModuleB", Shared.Constants.SystemModuleId);
    }

    [Fact]
    public void WithAlwaysSyncedModules_should_not_duplicate_the_system_module()
    {
        // Arrange
        var installedModules = new[] { "ModuleA", Shared.Constants.SystemModuleId };

        // Act
        var result = SyncDataHelpers.WithAlwaysSyncedModules(installedModules).ToList();

        // Assert
        result.Should().Equal("ModuleA", Shared.Constants.SystemModuleId);
    }
}
