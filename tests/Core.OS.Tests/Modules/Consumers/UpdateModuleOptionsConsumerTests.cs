using Core.OS.Modules;
using Core.OS.Modules.Consumers;
using Core.Shared.Modules;
using Core.Shared.Modules.Commands;
using Core.Shared.Modules.Events;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;
using Sdk.Modules;
using Sdk.Testing.Backend;
using TestModule.Backend;

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
        var result = await tester.TestCommand<UpdateModuleOptions, UpdateModuleOptionsConsumer, ModuleOptionsChanged>(command);

        // Assert
        result.Error.Should().BeNull();
        await _optionsStore.Received()
            .Store(ModuleIdResolver.ResolveId<TestBackendModule>(), Arg.Any<List<ModuleOptionDeclaration>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_send_event_with_error_if_store_fails()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new UpdateModuleOptions(ModuleIdResolver.ResolveId<TestBackendModule>(), []);

        _optionsStore.Store(ModuleIdResolver.ResolveId<TestBackendModule>(), Arg.Any<List<ModuleOptionDeclaration>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("store unavailable"));

        // Act — ADR-002: a failed store must not produce a success-shaped completion event.
        var result = await tester.TestCommand<UpdateModuleOptions, UpdateModuleOptionsConsumer, ModuleOptionsChanged>(command);

        // Assert
        result.Error.Should().NotBeNull();
        result.Error!.ErrorCode.Should().Be(ModuleErrorCodes.UpdateOptionsFailed);
        result.Error.Message.Should().Be("store unavailable");
        await _optionsStore.Received()
            .Store(ModuleIdResolver.ResolveId<TestBackendModule>(), Arg.Any<List<ModuleOptionDeclaration>>(), Arg.Any<CancellationToken>());
    }
}
