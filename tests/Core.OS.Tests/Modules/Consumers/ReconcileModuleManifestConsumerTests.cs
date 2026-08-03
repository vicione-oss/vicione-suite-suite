using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Core.OS.Instance;
using Core.OS.Modules;
using Core.OS.Modules.Consumers;
using Core.OS.Modules.Extensions;
using Core.Shared.Instance.Commands;
using Core.Shared.Modules.Commands;
using Core.Shared.Modules.Contracts;
using AwesomeAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Instance;
using Sdk.Modules;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Modules.Consumers;

public class ReconcileModuleManifestConsumerTests
{
    private readonly IModulePackageManifestStore _manifestStore = Substitute.For<IModulePackageManifestStore>();
    private readonly IModulePackageOperationStore _operationStore = Substitute.For<IModulePackageOperationStore>();
    private readonly MockFileSystem _fileSystem = new();
    private readonly InstanceOptions _instanceOptions;

    private string SignaturePath => _fileSystem.GetModuleReconcileSignatureFilePath(_instanceOptions);

    public ReconcileModuleManifestConsumerTests()
    {
        _instanceOptions = new InstanceOptions
        {
            HomeDirectory = _fileSystem.Path.GetFullPath("home"),
            CacheDirectory = "cache",
            BackupDirectory = "backup",
            Type = InstanceType.Slave,
        };
        _fileSystem.AddDirectory(_instanceOptions.HomeDirectory);
        _operationStore.EnqueueOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>()).Returns([]);
        SetupLocalManifest();
    }

    private Action<IBusRegistrationConfigurator> ConfigureServices(InstanceType type)
    {
        _instanceOptions.Type = type;
        return cfg =>
        {
            cfg.AddConsumer<ReconcileModuleManifestConsumer>();
            cfg.AddSingleton(_manifestStore);
            cfg.AddSingleton(_operationStore);
            cfg.AddSingleton<IFileSystem>(_fileSystem);
            cfg.AddSingleton(Options.Create(_instanceOptions));
        };
    }

    private void SetupLocalManifest(params ModuleDependencyPackage[] packages)
        => _manifestStore.Load(Arg.Any<CancellationToken>()).Returns(new ModulePackageManifest { Packages = [.. packages] });

    private static ModuleDependencyPackage Package(string name, string version = "1.0.0")
        => new() { Name = name, Version = version };

    private static ReconcileModuleManifest Command(params ModuleDependencyPackage[] desired)
        => new([.. desired]) { InstanceId = Guid.NewGuid() };

    [Theory]
    [InlineData(InstanceType.Master)]
    [InlineData(InstanceType.Standalone)]
    public async Task Should_do_nothing_when_not_a_slave(InstanceType instanceType)
    {
        // Arrange
        await using var tester = new MassTransitTester(ConfigureServices(instanceType));
        SetupLocalManifest();
        var command = Command(Package("ModuleA"));

        // Act
        await tester.TestInstanceDependentCommand<ReconcileModuleManifest, ReconcileModuleManifestConsumer>(command);

        // Assert
        await _operationStore.DidNotReceive().EnqueueOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>());
        (await tester.Harness.Sent.Any<ControlInstance>(TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task Should_do_nothing_when_manifest_already_matches()
    {
        // Arrange
        await using var tester = new MassTransitTester(ConfigureServices(InstanceType.Slave));
        SetupLocalManifest(Package("ModuleA"));
        var command = Command(Package("ModuleA"));

        // Act
        await tester.TestInstanceDependentCommand<ReconcileModuleManifest, ReconcileModuleManifestConsumer>(command);

        // Assert
        await _operationStore.DidNotReceive().EnqueueOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>());
        (await tester.Harness.Sent.Any<ControlInstance>(TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task Should_enqueue_install_for_missing_module_and_restart()
    {
        // Arrange
        await using var tester = new MassTransitTester(ConfigureServices(InstanceType.Slave));
        SetupLocalManifest();
        var command = Command(Package("ModuleA", "2.0.0"));

        // Act
        await tester.TestInstanceDependentCommand<ReconcileModuleManifest, ReconcileModuleManifestConsumer>(command);

        // Assert
        await _operationStore.Received(1).EnqueueOperations(
            Arg.Is<List<ModulePackageOperation>>(ops => ops!.Count == 1
                && ops[0].OperationKind == ModulePackageOperationKind.Install
                && ops[0].Package.Name == "ModuleA"
                && ops[0].Package.Version == "2.0.0"),
            Arg.Any<CancellationToken>());

        var restartSent = await tester.Harness.Sent.Any<ControlInstance>(
            k => k.Context.Message.InstanceId == command.InstanceId && k.Context.Message.Action == InstanceCommand.Restart,
            TestContext.Current.CancellationToken);
        restartSent.Should().BeTrue();
    }

    [Fact]
    public async Task Should_enqueue_uninstall_for_extra_module_and_restart()
    {
        // Arrange
        await using var tester = new MassTransitTester(ConfigureServices(InstanceType.Slave));
        SetupLocalManifest(Package("ModuleA"), Package("ModuleB"));
        var command = Command(Package("ModuleA"));

        // Act
        await tester.TestInstanceDependentCommand<ReconcileModuleManifest, ReconcileModuleManifestConsumer>(command);

        // Assert
        await _operationStore.Received(1).EnqueueOperations(
            Arg.Is<List<ModulePackageOperation>>(ops => ops!.Count == 1
                && ops[0].OperationKind == ModulePackageOperationKind.Uninstall
                && ops[0].Package.Name == "ModuleB"),
            Arg.Any<CancellationToken>());

        (await tester.Harness.Sent.Any<ControlInstance>(
            k => k.Context.Message.Action == InstanceCommand.Restart, TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task Should_ignore_version_drift_for_present_modules()
    {
        // Arrange
        await using var tester = new MassTransitTester(ConfigureServices(InstanceType.Slave));
        SetupLocalManifest(Package("ModuleA", "1.0.0"));
        var command = Command(Package("ModuleA", "2.0.0"));

        // Act
        await tester.TestInstanceDependentCommand<ReconcileModuleManifest, ReconcileModuleManifestConsumer>(command);

        // Assert: identity-only diff ignores version differences for modules present on both sides
        await _operationStore.DidNotReceive().EnqueueOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>());
        (await tester.Harness.Sent.Any<ControlInstance>(TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task Should_write_reconcile_signature_when_reconciling()
    {
        // Arrange
        await using var tester = new MassTransitTester(ConfigureServices(InstanceType.Slave));
        SetupLocalManifest();
        var command = Command(Package("ModuleB"), Package("ModuleA"));

        // Act
        await tester.TestInstanceDependentCommand<ReconcileModuleManifest, ReconcileModuleManifestConsumer>(command);

        // Assert: signature is the sorted "name@version" set of the desired packages
        _fileSystem.File.Exists(SignaturePath).Should().BeTrue();
        (await _fileSystem.File.ReadAllTextAsync(SignaturePath, TestContext.Current.CancellationToken))
            .Should().Be("ModuleA@1.0.0;ModuleB@1.0.0");
    }

    [Fact]
    public async Task Should_skip_reconcile_when_signature_matches_previous_attempt()
    {
        // Arrange: a previous attempt for the exact same desired set already ran
        await _fileSystem.File.WriteAllTextAsync(SignaturePath, "ModuleA@1.0.0", TestContext.Current.CancellationToken);
        await using var tester = new MassTransitTester(ConfigureServices(InstanceType.Slave));
        SetupLocalManifest();
        var command = Command(Package("ModuleA"));

        // Act
        await tester.TestInstanceDependentCommand<ReconcileModuleManifest, ReconcileModuleManifestConsumer>(command);

        // Assert: loop guard prevents another enqueue + restart
        await _operationStore.DidNotReceive().EnqueueOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>());
        (await tester.Harness.Sent.Any<ControlInstance>(TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task Should_clear_signature_when_manifest_matches()
    {
        // Arrange: a stale signature from a previous reconcile exists but the node is now converged
        await _fileSystem.File.WriteAllTextAsync(SignaturePath, "ModuleA@1.0.0", TestContext.Current.CancellationToken);
        await using var tester = new MassTransitTester(ConfigureServices(InstanceType.Slave));
        SetupLocalManifest(Package("ModuleA"));
        var command = Command(Package("ModuleA"));

        // Act
        await tester.TestInstanceDependentCommand<ReconcileModuleManifest, ReconcileModuleManifestConsumer>(command);

        // Assert
        _fileSystem.File.Exists(SignaturePath).Should().BeFalse();
    }
}
