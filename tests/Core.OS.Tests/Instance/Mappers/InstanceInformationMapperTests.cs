using System.Reflection;
using Core.OS.Instance.Commands;
using Core.OS.Instance.Mappers;
using Core.Shared.Instance.Contracts;
using Sdk.Instance;

namespace Core.OS.Tests.Instance.Mappers;

public class InstanceInformationMapperTests
{
    private static readonly HashSet<string> CommandOnlyProperties =
    [
        nameof(RegisterInstance.Configuration),
        nameof(RegisterInstance.CorrelationId),
        nameof(RegisterInstance.ForceSync),
        nameof(RegisterInstance.LastAppliedSequences)
    ];

    private static readonly HashSet<string> LifecycleProperties =
    [
        nameof(InstanceInformation.FirstTimeRegistered),
        nameof(InstanceInformation.LastRegistered),
        nameof(InstanceInformation.InRecoveryMode)
    ];

    [Fact]
    public void ToRegisterInstanceCommand_should_map_all_shared_properties()
    {
        var info = CreateFullInstanceInformation();
        var command = info.ToRegisterInstanceCommand(["mod"], [], []);

        var infoProps = GetSettableProperties<InstanceInformation>()
            .Except(LifecycleProperties)
            .ToHashSet();

        var commandProps = GetSettableProperties<RegisterInstance>()
            .Except(CommandOnlyProperties)
            .ToHashSet();

        var sharedNames = MapCommandPropertyToInfoProperty(commandProps);

        foreach (var infoProp in infoProps)
        {
            Assert.Contains(infoProp, sharedNames.Values.ToHashSet());
        }
    }

    [Fact]
    public void ToInstanceInformation_from_command_should_set_all_non_lifecycle_properties()
    {
        var command = CreateFullCommand();
        var result = command.ToInstanceInformation(DateTimeOffset.UtcNow);

        var expectedProps = GetSettableProperties<InstanceInformation>()
            .Except(LifecycleProperties)
            .Except([nameof(InstanceInformation.InRecoveryMode)])
            .ToList();

        foreach (var prop in expectedProps)
        {
            var value = typeof(InstanceInformation).GetProperty(prop)!.GetValue(result);
            Assert.False(IsDefault(value), $"Property '{prop}' was not set by ToInstanceInformation");
        }
    }

    [Fact]
    public void ApplyTo_from_command_should_update_all_non_lifecycle_properties()
    {
        var command = CreateFullCommand();
        var existing = new InstanceInformation { Id = command.InstanceId };

        command.ApplyTo(existing, DateTimeOffset.UtcNow);

        var expectedProps = GetSettableProperties<InstanceInformation>()
            .Except([nameof(InstanceInformation.FirstTimeRegistered), nameof(InstanceInformation.InRecoveryMode)])
            .ToList();

        foreach (var prop in expectedProps)
        {
            var value = typeof(InstanceInformation).GetProperty(prop)!.GetValue(existing);
            Assert.False(IsDefault(value), $"Property '{prop}' was not set by ApplyTo");
        }
    }

    [Fact]
    public void ApplyTo_from_interface_should_copy_all_non_lifecycle_properties()
    {
        var source = CreateFullInstanceInformation();
        source.FirstTimeRegistered = DateTimeOffset.UtcNow.AddDays(-10);
        source.LastRegistered = DateTimeOffset.UtcNow;

        var target = new InstanceInformation { Id = source.Id };

        ((IInstanceInformation)source).ApplyTo(target);

        var expectedProps = GetSettableProperties<InstanceInformation>()
            .Except([nameof(InstanceInformation.InRecoveryMode)])
            .ToList();

        foreach (var prop in expectedProps)
        {
            var sourceValue = typeof(InstanceInformation).GetProperty(prop)!.GetValue(source);
            var targetValue = typeof(InstanceInformation).GetProperty(prop)!.GetValue(target);
            Assert.Equal(sourceValue, targetValue);
        }
    }

    private static RegisterInstance CreateFullCommand() =>
        new()
        {
            InstanceId = Guid.NewGuid(),
            Type = InstanceType.Master,
            Name = "TestInstance",
            FormattedName = "{Test} Instance",
            Description = "A test instance",
            SerialNumber = "SN-001",
            SystemType = "Linux",
            SdkVersion = "1.0.0",
            BranchName = "main",
            InstalledModules = ["mod-a", "mod-b"],
            Configuration = [new("key", "value")],
            Version = "2.0.0",
            CorrelationId = Guid.NewGuid(),
            ForceSync = true,
            LastAppliedSequences = new() { ["ctx"] = 42 }
        };

    private static InstanceInformation CreateFullInstanceInformation() =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = InstanceType.Master,
            Name = "TestInstance",
            FormattedName = "{Test} Instance",
            Description = "A test instance",
            SerialNumber = "SN-001",
            SystemType = "Linux",
            SdkVersion = "1.0.0",
            BranchName = "main",
            InstalledModules = ["mod-a", "mod-b"],
            Version = "2.0.0",
            FirstTimeRegistered = DateTimeOffset.UtcNow.AddDays(-30),
            LastRegistered = DateTimeOffset.UtcNow
        };

    private static HashSet<string> GetSettableProperties<T>() =>
        typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite || p.GetMethod?.ReturnType != p.PropertyType)
            .Where(p => p.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() == null)
            .Select(p => p.Name)
            .ToHashSet();

    private static Dictionary<string, string> MapCommandPropertyToInfoProperty(HashSet<string> commandProps)
    {
        var map = new Dictionary<string, string>();
        foreach (var prop in commandProps)
        {
            var infoPropName = prop == nameof(RegisterInstance.InstanceId)
                ? nameof(InstanceInformation.Id)
                : prop;
            map[prop] = infoPropName;
        }
        return map;
    }

    private static bool IsDefault(object? value)
    {
        if (value is null) return true;
        var type = value.GetType();
        if (type == typeof(string)) return string.IsNullOrEmpty((string)value);
        if (type == typeof(Guid)) return (Guid)value == Guid.Empty;
        if (type.IsValueType) return value.Equals(Activator.CreateInstance(type));
        if (value is ICollection<string> list) return list.Count == 0;
        return false;
    }
}
