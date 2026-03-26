using AwesomeAssertions;
using Blazor.Shared.Module;
using Blazor.Shared.Module.ControlPanels.Models;
using Blazor.Shared.Module.Services;
using Core.Shared.Modules.Commands;
using Core.Shared.Modules.Contracts;
using Core.Shared.Modules.Events;
using Core.Shared.Modules.Requests;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.Infrastructure;
using Sdk.Instance;
using Sdk.Messaging;
using Sdk.Modules;
using Xunit;

namespace Blazor.Shared.Tests.Module.Services;

public class ModuleManagementServiceTests
{
    private const string MinSuiteVersion = "0.17.0";

    private const string TestClientModuleName = "ViciOne.Suite.TestClientModule";
    private const string TestClientModuleVersion = "1.0.0";
    private const string TestClientModuleUpdateVersion = "1.2.1";

    private const string AnotherModuleName = "ViciOne.Suite.AnotherModule";
    private const string AnotherModuleVersion = "1.3.2";

    private const string AvailableModulePackageName = "ViciOne.Suite.AvailableModule";
    private const string AvailableModuleVersion = "1.0.1";

    private readonly List<ModuleMetadataBundle> _moduleBundles = [
        new ModuleMetadataBundle {
            ModuleId = TestClientModuleName,
            Metadata = new ModuleMetadata {
                MinSuiteSdkVersion = MinSuiteVersion,
                Name = TestClientModuleName,
                Version = TestClientModuleVersion,
            },
            Installed = true,
            AvailableVersions = [
                TestClientModuleUpdateVersion,
                "0.7.0"
            ],
        },
        new ModuleMetadataBundle {
            ModuleId = AnotherModuleName,
            Metadata = new ModuleMetadata {
                MinSuiteSdkVersion = MinSuiteVersion,
                Name = AnotherModuleName,
                Version = AnotherModuleVersion
            },
            Installed = true,
            AvailableVersions = [
                AnotherModuleVersion
            ]
        },
        new ModuleMetadataBundle {
            ModuleId = AvailableModulePackageName,
            Metadata = new ModuleMetadata {
                MinSuiteSdkVersion = MinSuiteVersion,
                Name = AvailableModulePackageName,
                Version = AvailableModuleVersion,
                Options = [
                    new ModuleOptionDeclaration
                    {
                        Key = "Option1",
                        DefaultValue = "test1",
                        OptionType = ModuleOptionType.Text,
                    },
                    new ModuleOptionDeclaration
                    {
                        Key = "Option2",
                        DefaultValue = "2",
                        OptionType = ModuleOptionType.Number,
                    },
                    new ModuleOptionDeclaration
                    {
                        Key = "Option3",
                        OptionType = ModuleOptionType.Text,
                    },
                ]
            },
            Installed = false,
            AvailableVersions = [
                AnotherModuleVersion,
                "ci-234234"
            ]
        }];

    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly IInstanceInformationProvider _informationProvider = Substitute.For<IInstanceInformationProvider>();

    private ServiceProvider SetupServiceProvider()
        => new ServiceCollection()
            .AddSingleton(_mediator)
            .AddSingleton(_informationProvider)
            .AddSingleton<ModuleManagementService>()
            .BuildServiceProvider();

    private void SetupModuleMetadata(List<ModuleMetadataBundle>? bundles = null)
        => _mediator.Request<GetModuleMetadataBundlesRequest, GetModuleMetadataBundlesResponse>(
            Arg.Any<GetModuleMetadataBundlesRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetModuleMetadataBundlesResponse(bundles ?? []));

    public sealed class GetModuleMetadata : ModuleManagementServiceTests
    {
        [Fact]
        public async Task Should_preset_available_modules_option_values_with_defaults()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var service = serviceProvider.GetRequiredService<ModuleManagementService>();
            SetupModuleMetadata(_moduleBundles);

            // Act
            var results = await service.GetMetadata(cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            var available = results.First(k => k.ModuleId == AvailableModulePackageName);
            foreach (var option in available.EditOptions.Values)
            {
                if (string.IsNullOrWhiteSpace(option.DefaultValue))
                    continue;

                option.Value.Should().Be(option.DefaultValue);
            }

            available.EditOptions.Values.Should().ContainSingle(k => k.Value == null, "the other 2 options have their default values");
        }

        [Fact]
        public async Task Should_return_empty_result_if_no_modules_are_installed_or_available()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var service = serviceProvider.GetRequiredService<ModuleManagementService>();
            SetupModuleMetadata();

            // Act
            var results = await service.GetMetadata(true, TestContext.Current.CancellationToken);

            // Assert
            results.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_return_installed_modules()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var service = serviceProvider.GetRequiredService<ModuleManagementService>();
            SetupModuleMetadata(_moduleBundles);
            var models = ModuleMetadataModelFactory.CreateModels(_moduleBundles);

            // Act
            var results = await service.GetMetadata(cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            results.Select(k => k).Should().BeEquivalentTo(models);
        }

        [Fact]
        public async Task Should_thrown_on_request_error()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var service = serviceProvider.GetRequiredService<ModuleManagementService>();

            _mediator.Request<GetModuleMetadataBundlesRequest, GetModuleMetadataBundlesResponse>(
            Arg.Any<GetModuleMetadataBundlesRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetModuleMetadataBundlesResponse([], new ErrorInfo(1, "Request failed")));

            // Act
            var action = () => service.GetMetadata(cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            await action.Should().ThrowAsync<InvalidOperationException>();
        }
    }

    public sealed class UpdateOptionsTests : ModuleManagementServiceTests
    {
        [Fact]
        public async Task Should_send_create_command_and_return_success()
        {
            // Arrange
            var model = CreateModel();
            using var service = new ModuleManagementService(_mediator);

            SetupSendAndSimulateEvent<UpdateModuleOptions, ModuleOptionsChanged>(
                _mediator, service,
                cmd => new ModuleOptionsChanged(model.ModuleId) { CorrelationId = cmd.CorrelationId });

            // Act
            var result = await service.UpdateOptions(model.ModuleId, [], TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<ModuleManagementServiceSuccessResult>();
            await _mediator.Received(1).Send(Arg.Any<UpdateModuleOptions>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_return_error_when_backend_reports_error()
        {
            // Arrange
            var model = CreateModel();
            using var service = new ModuleManagementService(_mediator);

            SetupSendAndSimulateEvent<UpdateModuleOptions, ModuleOptionsChanged>(
                _mediator, service,
                cmd => new ModuleOptionsChanged(model.ModuleId, new ErrorInfo(100, "create failed"))
                { CorrelationId = cmd.CorrelationId });

            // Act
            var result = await service.UpdateOptions(model.ModuleId, [], TestContext.Current.CancellationToken);

            // Assert
            var errorResult = result.Should().BeOfType<ModuleManagementServiceErrorResult>().Subject;
            errorResult.ErrorMessage.Should().Be("create failed");
        }

        [Fact]
        public async Task Should_fire_option_changed_event_on_success()
        {
            // Arrange
            var model = CreateModel();
            using var service = new ModuleManagementService(_mediator);

            SetupSendAndSimulateEvent<UpdateModuleOptions, ModuleOptionsChanged>(
                _mediator, service,
                cmd => new ModuleOptionsChanged(model.ModuleId) { CorrelationId = cmd.CorrelationId });

            string? moduleId = null;
            CancellationToken? cancellationToken = null;
            service.OptionsChanged += (changed, token) => { moduleId = changed.ModuleId; cancellationToken = token; return Task.CompletedTask; };

            // Act
            await service.UpdateOptions(model.ModuleId, [], TestContext.Current.CancellationToken);

            // Assert            
            moduleId.Should().NotBeNullOrEmpty();
        }
    }

    public sealed class UpdateOperationsTests : ModuleManagementServiceTests
    {
        private readonly ModuleDependencyPackage _package = new()
        {
            Name = "ViciOne.TestPackage",
            Version = "1.4.0"
        };

        [Fact]
        public async Task Should_send_create_command_and_return_success()
        {
            // Arrange
            var model = CreateModel();
            var operation = new ModulePackageOperation(_package, ModulePackageOperationKind.Uninstall);
            using var service = new ModuleManagementService(_mediator);

            SetupSendAndSimulateEvent<UpdateModulePackageOperations, ModulePackageOperationsChanged>(
                _mediator, service,
                cmd => new ModulePackageOperationsChanged([new ModulePackageChange(CrudAction.Deleted, operation)]) { CorrelationId = cmd.CorrelationId });

            // Act
            var result = await service.UpdateOperations([operation], TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<ModuleManagementServiceSuccessResult>();
            await _mediator.Received(1).Send(Arg.Any<UpdateModulePackageOperations>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_return_error_when_backend_reports_error()
        {
            // Arrange
            var model = CreateModel();
            var operation = new ModulePackageOperation(_package, ModulePackageOperationKind.Uninstall);
            using var service = new ModuleManagementService(_mediator);

            SetupSendAndSimulateEvent<UpdateModulePackageOperations, ModulePackageOperationsChanged>(
                _mediator, service,
                cmd => new ModulePackageOperationsChanged([], new ErrorInfo(100, "create failed"))
                { CorrelationId = cmd.CorrelationId });

            // Act
            var result = await service.UpdateOperations([operation], TestContext.Current.CancellationToken);

            // Assert
            var errorResult = result.Should().BeOfType<ModuleManagementServiceErrorResult>().Subject;
            errorResult.ErrorMessage.Should().Be("create failed");
        }

        [Fact]
        public async Task Should_fire_operations_changed_event_on_success()
        {
            // Arrange
            var model = CreateModel();
            var operation = new ModulePackageOperation(_package, ModulePackageOperationKind.Uninstall);
            using var service = new ModuleManagementService(_mediator);

            SetupSendAndSimulateEvent<UpdateModulePackageOperations, ModulePackageOperationsChanged>(
                _mediator, service,
                cmd => new ModulePackageOperationsChanged([new ModulePackageChange(CrudAction.Deleted, operation)])
                { CorrelationId = cmd.CorrelationId });

            IReadOnlyCollection<ModulePackageChange>? changes = null;
            CancellationToken? cancellationToken = null;
            service.OperationsChanged += (changed, token) => { changes = changed.Changes; cancellationToken = token; return Task.CompletedTask; };

            // Act
            await service.UpdateOperations([operation], TestContext.Current.CancellationToken);

            // Assert
            changes.Should().NotBeNullOrEmpty();
            changes.Should().ContainSingle(c => c.Action == CrudAction.Deleted && c.Operation.Package == _package);
        }
    }

    private static ModuleMetadataBundle CreateModel()
        => new()
        {
            ModuleId = "ViciOne.TestPackage",
            Metadata = new ModuleMetadata() { MinSuiteSdkVersion = "1.0.0", Name = "ViciOne.TestPackage", Version = "1.2.3" },
            Installed = true
        };

    private static void SetupSendAndSimulateEvent<TCommand, TEvent>(
            IUiMediator mediator,
            ModuleManagementService service,
            Func<TCommand, TEvent> eventFactory)
            where TCommand : class, ICommand
            where TEvent : class, IEvent
            => mediator.When(m => m.Send(Arg.Any<TCommand>(), Arg.Any<CancellationToken>()))
                .Do(async callinfo =>
                {
                    var command = callinfo.Arg<TCommand>();
                    var @event = eventFactory(command);
                    if (@event is ModulePackageOperationsChanged operationsChanged)
                    {
                        var context = new ClientContext<ModulePackageOperationsChanged>(operationsChanged, command.CorrelationId);
                        await service.Consume(context, CancellationToken.None);
                    }
                    else if (@event is ModuleOptionsChanged optionsChanged)
                    {
                        var context = new ClientContext<ModuleOptionsChanged>(optionsChanged, command.CorrelationId);
                        await service.Consume(context, CancellationToken.None);
                    }
                });
}
