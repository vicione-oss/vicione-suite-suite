using AutoFixture;
using AwesomeAssertions.Equivalency;
using Core.OS.Instance.Commands;
using Core.OS.Instance.Mappers;
using Core.Shared.Instance.Contracts;
using Sdk.Instance;

namespace Core.OS.Tests.Instance.Mappers;

public class InstanceInformationMapperTests
{
    [Fact]
    public void ToRegisterInstanceCommand_should_map_all_shared_properties()
    {
        // Arrange
        var info = CreateInstanceInformation();
        List<string> loadedModules = ["loaded-module"];

        // Act
        var command = info.ToRegisterInstanceCommand(loadedModules, [], []);

        // Assert
        command.Should().BeEquivalentTo(info, o => o
            .WithMapping<InstanceInformation, RegisterInstance>(i => i.Id, c => c.InstanceId)
            .Excluding(i => i.FirstTimeRegistered)
            .Excluding(i => i.LastRegistered)
            .Excluding(i => i.InRecoveryMode)
            .Excluding(i => i.InstalledModules));
        command.InstalledModules.Should().Equal(loadedModules);
    }

    [Fact]
    public void ToInstanceInformation_from_command_should_set_all_non_lifecycle_properties()
    {
        // Arrange
        var command = CreateCommand();
        var registrationTime = DateTimeOffset.UtcNow;

        // Act
        var result = command.ToInstanceInformation(registrationTime);

        // Assert
        result.Should().BeEquivalentTo(command, MatchingSharedProperties);
        result.FirstTimeRegistered.Should().Be(registrationTime);
        result.LastRegistered.Should().Be(registrationTime);
    }

    [Fact]
    public void ApplyTo_from_command_should_update_all_non_lifecycle_properties()
    {
        // Arrange
        var command = CreateCommand();
        var existing = CreateInstanceInformation(command.InstanceId);
        var firstTimeRegistered = existing.FirstTimeRegistered;
        var registrationTime = DateTimeOffset.UtcNow;

        // Act
        command.ApplyTo(existing, registrationTime);

        // Assert
        existing.Should().BeEquivalentTo(command, MatchingSharedProperties);
        existing.FirstTimeRegistered.Should().Be(firstTimeRegistered);
        existing.LastRegistered.Should().Be(registrationTime);
    }

    [Fact]
    public void ApplyTo_from_interface_should_copy_all_non_lifecycle_properties()
    {
        // Arrange
        var source = CreateInstanceInformation();
        var target = CreateInstanceInformation(source.Id);
        source.InRecoveryMode = false;
        target.InRecoveryMode = true;

        // Act
        ((IInstanceInformation)source).ApplyTo(target);

        // Assert
        target.Should().BeEquivalentTo(source, o => o.Excluding(i => i.InRecoveryMode));
        target.InRecoveryMode.Should().BeTrue();
    }

    /// <summary>
    /// Fills every settable property, so a property added later is non-default without touching this test.
    /// <see cref="InstanceType"/> is set explicitly because AutoFixture starts with its default value.
    /// </summary>
    private static InstanceInformation CreateInstanceInformation(Guid? id = null) =>
        new Fixture().Build<InstanceInformation>()
            .With(i => i.Id, id ?? Guid.NewGuid())
            .With(i => i.Type, InstanceType.Master)
            .Create();

    private static RegisterInstance CreateCommand() =>
        new Fixture().Build<RegisterInstance>()
            .With(c => c.Type, InstanceType.Master)
            .Create();

    /// <summary>
    /// Command-only members such as <see cref="RegisterInstance.Configuration"/> have no counterpart and are skipped.
    /// </summary>
    private static EquivalencyOptions<RegisterInstance> MatchingSharedProperties(EquivalencyOptions<RegisterInstance> options) =>
        options
            .WithMapping<RegisterInstance, InstanceInformation>(c => c.InstanceId, i => i.Id)
            .ExcludingMissingMembers();
}
