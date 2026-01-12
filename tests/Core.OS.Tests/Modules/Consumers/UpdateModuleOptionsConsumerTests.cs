using Core.OS.Modules;
using Core.OS.Modules.Consumers;
using Core.Shared.Modules.Commands;
using Core.Shared.Modules.Events;
using AwesomeAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sdk.Modules;
using Sdk.Testing.Backend;
using TestModule.Backend;
using Xunit;

namespace Core.OS.Tests.Modules.Consumers;

public class UpdateModuleOptionsConsumerTests
{
    private readonly IModuleOptionsStore _optionsStore = Substitute.For<IModuleOptionsStore>();
    private readonly ILogger<UpdateModuleOptionsConsumer> _logger = Substitute.For<ILogger<UpdateModuleOptionsConsumer>>();
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public UpdateModuleOptionsConsumerTests() =>
        _configureServices = (cfg) =>
        {
            cfg.AddSingleton(_optionsStore);
            cfg.AddSingleton(_logger);
            cfg.AddConsumer<UpdateModuleOptionsConsumer>();
        };

    [Fact]
    public async Task Should_store_module_options()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new UpdateModuleOptions(ModuleIdResolver.ResolveId<TestBackendModule>(), []);

        // Act
        var result = await tester.TestCommand<UpdateModuleOptions, UpdateModuleOptionsConsumer, ModuleOptionsUpdatedEvent>(command);

        // Assert
        result.Success.Should().BeTrue();
        await _optionsStore.Received()
            .Store(ModuleIdResolver.ResolveId<TestBackendModule>(), Arg.Any<List<ModuleOptionDeclaration>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_send_event_if_store_fails()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new UpdateModuleOptions(ModuleIdResolver.ResolveId<TestBackendModule>(), []);

        _optionsStore.Store(ModuleIdResolver.ResolveId<TestBackendModule>(), Arg.Any<List<ModuleOptionDeclaration>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException());

        // Act
        var result = await tester.TestCommand<UpdateModuleOptions, UpdateModuleOptionsConsumer, ModuleOptionsUpdatedEvent>(command);

        // Assert
        result.Success.Should().BeFalse();
        await _optionsStore.Received()
            .Store(ModuleIdResolver.ResolveId<TestBackendModule>(), Arg.Any<List<ModuleOptionDeclaration>>(), Arg.Any<CancellationToken>());
    }
}
