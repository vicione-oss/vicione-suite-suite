using System.Text.Json;
using Core.OS.HostManagement;
using Core.OS.HostManagement.Consumers;
using Core.Shared.HostManagement.Commands;
using Core.Shared.HostManagement.Events;
using Core.OS.Tests.HostManagement.Extensions;
using Core.Shared.UserManagement.Contracts;
using HostManagement.Shared.Capabilities;
using HostManagement.Shared.Communication;
using HostManagement.Shared.Communication.Capabilities;
using HostManagement.Shared.Communication.Contracts;
using HostManagement.Shared.Communication.Enums;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.HostManagement.Consumers;

public sealed class UpdateSystemConsumerTests
{
    private readonly IPipeClient _pipeClient = Substitute.For<IPipeClient>();
    private readonly IQueryableUserStore<SuiteUser> _userStore = Substitute.For<IQueryableUserStore<SuiteUser>>();

    private void SetupServices(IBusRegistrationConfigurator cfg)
    {
        cfg.AddConsumer<UpdateSystemConsumer>();
        cfg.AddSingleton(_pipeClient);
        cfg.AddSingleton(_userStore);
        cfg.AddSingleton(Substitute.For<IOptions<IdentityOptions>>());
        cfg.AddSingleton(Substitute.For<IPasswordHasher<SuiteUser>>());
        cfg.AddSingleton(Substitute.For<IUserValidator<SuiteUser>>());
        cfg.AddSingleton(Substitute.For<IPasswordValidator<SuiteUser>>());
        cfg.AddSingleton(Substitute.For<ILookupNormalizer>());
        cfg.AddSingleton(Substitute.For<ILogger<UserManager<SuiteUser>>>);
        cfg.AddSingleton(Substitute.For<ILogger<UpdateSystemConsumer>>());
        cfg.AddSingleton<UserManager<SuiteUser>>(svc
            => new UserManager<SuiteUser>(
                _userStore,
                svc.GetRequiredService<IOptions<IdentityOptions>>(),
                svc.GetRequiredService<IPasswordHasher<SuiteUser>>(),
                svc.GetServices<IUserValidator<SuiteUser>>(),
                svc.GetServices<IPasswordValidator<SuiteUser>>(),
                svc.GetRequiredService<ILookupNormalizer>(),
                new IdentityErrorDescriber(),
                svc,
                svc.GetRequiredService<ILogger<UserManager<SuiteUser>>>()
            ));
    }

    [Fact]
    public async Task Should_send_update_system_request_to_host_management()
    {
        // Arrange        
        await using var tester = new MassTransitTester(SetupServices);

        var command = new UpdateSystem("some/file/path");
        SetupUpdateSystemRequestSuccess(command);

        // Act
        await tester.TestInstanceDependentCommand<UpdateSystem, UpdateSystemConsumer>(command);

        // Assert
        await _pipeClient.Received().SendRequest(Topics.UpdateSystem,
                command.FilePath,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_send_update_system_started_event_on_success()
    {
        // Arrange        
        await using var tester = new MassTransitTester(SetupServices);

        var command = new UpdateSystem("some/file/path");
        SetupUpdateSystemRequestSuccess(command);

        // Act
        await tester.TestInstanceDependentCommand<UpdateSystem, UpdateSystemConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<UpdateSystemStarted>(TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task Should_send_update_system_error_event_on_failure()
    {
        // Arrange        
        await using var tester = new MassTransitTester(SetupServices);

        var command = new UpdateSystem("some/file/path");

        // Act
        await tester.TestInstanceDependentCommand<UpdateSystem, UpdateSystemConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<UpdateSystemStarted>(r =>
            r.Context.Message.ErrorInfo != null, TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task Should_keep_logins_and_send_error_event_when_update_system_is_disabled()
    {
        // Arrange
        await using var tester = new MassTransitTester(SetupServices);

        var command = new UpdateSystem("some/file/path");
        SetupUpdateSystemRequestSuccess(command);
        var capabilities = new SupportedCapabilities();
        capabilities.Topics.UpdateSystem = CapabilityStatus.Disabled;
        _pipeClient.SetupGetSupportedCapabilitiesResult(OperationStatus.Success, capabilities);

        // Act
        await tester.TestInstanceDependentCommand<UpdateSystem, UpdateSystemConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<UpdateSystemStarted>(r =>
            r.Context.Message.ErrorInfo?.ErrorCode == UpdateSystemStarted.UpdateSystemDisabled, TestContext.Current.CancellationToken)).Should().BeTrue();
        _ = _userStore.DidNotReceive().Users;
        await _pipeClient.DidNotReceive().SendRequest(Topics.UpdateSystem, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private void SetupUpdateSystemRequestSuccess(UpdateSystem command)
    {
        var result = new SystemControlResult
        {
            Status = OperationStatus.Success,
            Message = "Message"
        };
        _pipeClient.SendRequest(Topics.UpdateSystem,
                command.FilePath,
                Arg.Any<CancellationToken>()).Returns(JsonSerializer.Serialize<SystemControlResult>(result, SourceGenerationContext.Default.SystemControlResult));

    }
}
